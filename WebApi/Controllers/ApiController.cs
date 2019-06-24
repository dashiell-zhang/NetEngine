using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using WebApi.Models;
using WebApi.Libraries;
using WebApi.Actions;
using Methods.Json;

namespace WebApi.Controllers
{


    [Route("[controller]")]
    [ApiController]
    public class ApiController : ControllerBase
    {
        /// <summary>
        /// get请求接口
        /// </summary>
        /// <remarks>
        /// 例子:
        /// Get api/Values/1
        /// </remarks>
        /// <param name="json">业务数据</param>
        /// <returns></returns>
        [HttpGet("id")]
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


        [HttpGet("{name}")]
        public string ceshi(string name)
        {
            return name;
        }


        /// <summary>
        /// post请求接口
        /// </summary>
        /// <param name="run"></param>
        /// <returns></returns>
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
