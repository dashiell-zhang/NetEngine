using Common;
using Microsoft.Extensions.Caching.Distributed;
using WebAPI.Libraries.WeiXin.App.Models;

namespace WebAPI.Libraries.WeiXin.App
{
    public class WeiXinHelper
    {
        private readonly string appid;

        private readonly string appsecret;


        public WeiXinHelper(string in_appid, string in_secret)
        {
            appid = in_appid;
            appsecret = in_secret;
        }




        /// <summary>
        /// 获取 AccessToken
        /// </summary>
        /// <returns></returns>
        public (string accessToken, string openId) GetAccessToken(IDistributedCache distributedCache, HttpClient httpClient, string code)
        {
            string? token = distributedCache.GetString("wxappaccesstoken" + code);
            string? openid = distributedCache.GetString("wxappopenid" + code);

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(openid))
            {

                string url = "https://api.weixin.qq.com/sns/oauth2/access_token?appid=" + appid + "&secret=" + appsecret + "&code=" + code + "&grant_type=authorization_code";

                var returnJson = httpClient.PostAsync(url, "", "form").Result.Content.ReadAsStringAsync().Result;

                var retToken = JsonHelper.GetValueByKey(returnJson, "access_token");
                var retOpenId = JsonHelper.GetValueByKey(returnJson, "openid");

                if (retToken != null && retOpenId != null)
                {
                    token = retToken;
                    openid = retOpenId;
                }
            }

            if (token != null && openid != null)
            {

                distributedCache.Set("wxappaccesstoken" + code, token, TimeSpan.FromSeconds(7100));
                distributedCache.Set("wxappopenid" + code, openid, TimeSpan.FromSeconds(7100));

                return (token, openid);
            }
            else
            {
                throw new Exception("获取 AccessToken 失败");
            }

        }



        /// <summary>
        /// 获取用户信息
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="accessToken"></param>
        /// <param name="openId"></param>
        /// <returns></returns>
        public DtoGetUserInfo GetUserInfo(HttpClient httpClient, string accessToken, string openId)
        {
            string url = "https://api.weixin.qq.com/sns/userinfo?access_token=" + accessToken + "&openid=" + openId;

            var returnJson = httpClient.PostAsync(url, "", "form").Result.Content.ReadAsStringAsync().Result;  

            var userInfo = JsonHelper.JsonToObject<DtoGetUserInfo>(returnJson);

            return userInfo;
        }
    }
}
