using Newtonsoft.Json;
using System;


namespace Cms.Libraries.Ueditor
{

    /// <summary>
    /// Handler 的摘要说明
    /// </summary>
    public abstract class Handler
    {


        public abstract string Process();

        protected string WriteJson(object response)
        {



            string jsonpCallback = "";
            //string jsonpCallback = Request["callback"],
            string json = JsonConvert.SerializeObject(response);
            if (String.IsNullOrWhiteSpace(jsonpCallback))
            {
                return json;
            }
            else
            {
                return String.Format("{0}({1});", jsonpCallback, json);
            }

        }
    }

}