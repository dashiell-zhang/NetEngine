using Common;
using Microsoft.AspNetCore.Mvc.Filters;
using Shared.Model.AppSetting;
using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using WebAPI.Core.Attributes;

namespace WebAPI.Core.Filters
{


    /// <summary>
    /// RSA解密过滤器
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RSADecryptFilter : Attribute, IActionFilter
    {


        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {

            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<RSADecryptFilter>>();
            var rsaSetting = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>().GetRequiredSection("RSA").Get<RSASetting>();

            string? privateKey = rsaSetting?.PrivateKey;

            if (privateKey != null)
            {
                foreach (var item in context.ActionArguments)
                {
                    var bindingSource = context.ActionDescriptor.Parameters.Where(t => t.Name == item.Key).Select(t => t.BindingInfo?.BindingSource).FirstOrDefault();

                    if (bindingSource != null)
                    {
                        if (bindingSource.IsFromRequest)
                        {
                            LoopDetection(item.Value);
                        }
                    }
                }
            }
            else
            {
                throw new Exception("私钥获取异常");
            }

            object? LoopDetection(object? objValue)
            {
                if (objValue != null)
                {

                    if (objValue.GetType().Name == "Dictionary`2")
                    {
                        if (objValue is IDictionary objValueDictionary)
                        {
                            foreach (var item in objValueDictionary.Values)
                            {
                                LoopDetection(item);
                            }
                        }
                        else
                        {
                            throw new Exception("IDictionary 模型转换失败");
                        }
                    }
                    else if (objValue.GetType().Name == "List`1" || objValue.GetType().BaseType?.Name == "Array")
                    {
                        if (objValue is ICollection objValueList)
                        {
                            foreach (var item in objValueList)
                            {
                                LoopDetection(item);
                            }
                        }
                        else
                        {
                            throw new Exception("ICollection 模型转换失败");
                        }
                    }
                    else
                    {
                        var propertyInfos = objValue.GetType().GetProperties();

                        foreach (var propertyInfo in propertyInfos)
                        {
                            if (propertyInfo.PropertyType == typeof(string))
                            {
                                DecryptData(objValue, propertyInfo);
                            }
                            else
                            {
                                if (propertyInfo.PropertyType.Namespace != "System")
                                {
                                    var tempObjValue = propertyInfo.GetValue(objValue);

                                    LoopDetection(tempObjValue);
                                }
                            }
                        }
                    }

                }
                return objValue;
            }

            void DecryptData(object objValue, PropertyInfo propertyInfo)
            {
                if (propertyInfo?.GetCustomAttribute<RSAEncryptedAttribute>() != null)
                {
                    string? encryptData = propertyInfo.GetValue(objValue)?.ToString();

                    if (encryptData != null)
                    {
                        try
                        {
                            string decryptData = CryptoHelper.RSADecrypt(privateKey, encryptData, "base64", RSAEncryptionPadding.OaepSHA256);
                            propertyInfo.SetValue(objValue, decryptData);
                        }
                        catch
                        {
#if DEBUG
                            logger.LogError("无效的 密文");
#else
                            throw new Exception("无效的 密文");
#endif
                        }
                    }

                }
            }
        }



        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
