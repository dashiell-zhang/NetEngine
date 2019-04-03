using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApi.Models;
using WebApi.Models.Data;
using Methods.Json;

namespace WebApi.Actions
{
    public class TestAction
    {
        

        public static RunResult Run(string data)
        {
            RunResult runResult = new RunResult();

            var request = JsonHelper.JSONToObject<Test.Request>(data);

            var response = new Test.Response();


            runResult.status = 1;
            runResult.msg = "ok";
            runResult.data = response;


            return runResult;
        }
    }
}
