using Common;

namespace AdminApi.Libraries.Ueditor
{

    /// <summary>
    /// Handler 的摘要说明
    /// </summary>
    public abstract class Handler
    {


        public abstract string Process();

        protected string WriteJson(object response)
        {
            string json = JsonHelper.ObjectToJson(response);
            return json;
        }
    }

}