using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Methods;
using WebApi.Actions;
using Methods.Json;

namespace WebApi.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {

        [HttpGet]
        public RunResult Get(string json)
        {
            RunResult result = new RunResult();

            if (json != null)
            {

                Run run = JsonHelper.JSONToObject<Run>(json);

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
            }
            else
            {
                result.status = 0;
                result.msg = "未收到任何数据";
            }
            return result;
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
