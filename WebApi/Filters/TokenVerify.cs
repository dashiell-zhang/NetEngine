using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Models.DataBases.WebCore;
using System;
using System.Linq;
using System.Security.Claims;

namespace WebApi.Filters
{

    public class TokenVerify : Attribute, IActionFilter
    {


        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {

            var exp = Convert.ToInt64(Methods.Verify.JwtToken.GetClaims("exp"));

            var exptime = Methods.DataTime.DateTimeHelper.UnixToTime(exp);

            if (exptime < DateTime.Now)
            {
                var tokenid = Methods.Verify.JwtToken.GetClaims("tokenid");

                using (webcoreContext db = new webcoreContext())
                {
                    var tokeninfo = db.TUserToken.Where(t => t.Id == tokenid).FirstOrDefault();

                    if (tokeninfo == null)
                    {
                        context.HttpContext.Response.StatusCode = 401;

                        context.Result = new JsonResult(new { errMsg = "非法 Token ！" });
                    }
                }
            }
        }


        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {

            var exp = Convert.ToInt64(Methods.Verify.JwtToken.GetClaims("exp"));

            var exptime = Methods.DataTime.DateTimeHelper.UnixToTime(exp);

            if (exptime < DateTime.Now)
            {

                var tokenid = Methods.Verify.JwtToken.GetClaims("tokenid");
                var userid = Methods.Verify.JwtToken.GetClaims("userid");

                using (webcoreContext db = new webcoreContext())
                {

                    var tokeninfo = db.TUserToken.Where(t => t.Id == tokenid).FirstOrDefault();

                    db.TUserToken.Remove(tokeninfo);

                    TUserToken userToken = new TUserToken();
                    userToken.Id = Guid.NewGuid().ToString();
                    userToken.UserId = userid;
                    userToken.CreateTime = DateTime.Now;

                    db.TUserToken.Add(userToken);

                    var oldtime = DateTime.Now.AddDays(-7);
                    var oldlist = db.TUserToken.Where(t => t.CreateTime < oldtime).ToList();
                    db.TUserToken.RemoveRange(oldlist);

                    db.SaveChanges();

                    var claim = new Claim[]{
                        new Claim("tokenid",userToken.Id),
                             new Claim("userid",userid)
                        };


                    var token = Methods.Verify.JwtToken.GetToken(claim);

                    context.HttpContext.Response.Headers.Add("NewToken", token);
                }
            }

        }
    }
}
