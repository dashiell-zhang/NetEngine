using Microsoft.AspNetCore.Mvc.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Cms.Filters
{


    public class IsLogin : Attribute, IActionFilter
    {
        void IActionFilter.OnActionExecuted(ActionExecutedContext context)
        {

        }

        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {

            if (!string.IsNullOrEmpty(context.HttpContext.Session.GetString("userid")))
            {
                //成功登录

            }
            else
            {
                //跳转到登录页面 
                context.HttpContext.Response.Redirect("/User/Login/");
            }

        }
    }
}
