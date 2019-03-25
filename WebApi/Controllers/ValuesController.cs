using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Methods;
using WebApi.Actions;

namespace WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {

        [HttpGet]
        public string Get()
        {
            return "msg:接口不支持get请求";
        }




        [HttpPost]
        public RunResult Post([FromBody]Run run)
        {
            RunResult result = new RunResult();

            if (Public.GetKey(run) == run.key)
            {
                switch (run.run)
                {
                    
                    case "Test":
                        return TestAction.Run(run.data);


                    default:
                        result.status = 0;
                        result.msg = "未找到" + run.run + "方法";
                        break;
                }
            }
            else
            {
                result.status = 0;
                result.msg = "data与key不匹配";
            }

            return result;
        }

    }
}
