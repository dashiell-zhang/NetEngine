namespace AdminAPI.Libraries.Ueditor
{

    /// <summary>
    /// Config 的摘要说明
    /// </summary>
    public class ConfigHandler : Handler
    {


        public override string Process(string fileServerUrl)
        {
            return WriteJson(Config.Items(fileServerUrl));
        }
    }

}