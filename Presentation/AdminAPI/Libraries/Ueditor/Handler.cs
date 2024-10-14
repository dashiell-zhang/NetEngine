﻿using Common;

namespace AdminAPI.Libraries.Ueditor
{

    /// <summary>
    /// Handler 的摘要说明
    /// </summary>
    public abstract class Handler
    {


        public abstract string Process(string fileServerUrl);

        protected string WriteJson(object response)
        {
            string json = JsonHelper.ObjectToJson(response);
            return json;
        }
    }

}