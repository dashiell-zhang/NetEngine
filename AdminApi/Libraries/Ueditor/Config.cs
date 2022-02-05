using System.Text.Json;

namespace AdminApi.Libraries.Ueditor
{
    /// <summary>
    /// Config 的摘要说明
    /// </summary>
    public static class Config
    {
        private readonly static bool noCache = true;

        private static JsonDocument BuildItems()
        {
            var json = @"{
                              /* 上传图片配置项 */
                              ""imageActionName"": ""uploadimage"", /* 执行上传图片的action名称 */
                              ""imageFieldName"": ""upfile"", /* 提交的图片表单名称 */
                              ""imageMaxSize"": 2147483647, /* 上传大小限制，单位B */
                              ""imageAllowFiles"": [ "".png"", "".jpg"", "".jpeg"", "".gif"", "".bmp"" ], /* 上传图片格式显示 */
                              ""imageCompressEnable"": true, /* 是否压缩图片,默认是true */
                              ""imageCompressBorder"": 1600, /* 图片压缩最长边限制 */
                              ""imageInsertAlign"": ""none"", /* 插入的图片浮动方式 */
                              ""imageUrlPrefix"": ""FileServerUrl"", /* 图片访问路径前缀 */
                              ""imagePathFormat"": ""/uploads/{yyyy}/{mm}/{dd}/{time}{rand:6}"", /* 上传保存路径,可以自定义保存路径和文件名格式 */
                            
                              /* 涂鸦图片上传配置项 */
                              ""scrawlActionName"": ""uploadscrawl"", /* 执行上传涂鸦的action名称 */
                              ""scrawlFieldName"": ""upfile"", /* 提交的图片表单名称 */
                              ""scrawlPathFormat"": ""/uploads/{yyyy}/{mm}/{dd}/{time}{rand:6}"", /* 上传保存路径,可以自定义保存路径和文件名格式 */
                              ""scrawlMaxSize"": 2147483647, /* 上传大小限制，单位B */
                              ""scrawlUrlPrefix"": ""FileServerUrl"", /* 图片访问路径前缀 */
                              ""scrawlInsertAlign"": ""none"",
                            
                              /* 截图工具上传 */
                              ""snapscreenActionName"": ""uploadimage"", /* 执行上传截图的action名称 */
                              ""snapscreenPathFormat"": ""/uploads/{yyyy}/{mm}/{dd}/{time}{rand:6}"", /* 上传保存路径,可以自定义保存路径和文件名格式 */
                              ""snapscreenUrlPrefix"": ""FileServerUrl"", /* 图片访问路径前缀 */
                              ""snapscreenInsertAlign"": ""none"", /* 插入的图片浮动方式 */
                            
                              /* 抓取远程图片配置 */
                              ""catcherLocalDomain"": [ ""127.0.0.1"", ""localhost"", ""img.baidu.com"" ],
                              ""catcherActionName"": ""catchimage"", /* 执行抓取远程图片的action名称 */
                              ""catcherFieldName"": ""source"", /* 提交的图片列表表单名称 */
                              ""catcherPathFormat"": ""/uploads/{yyyy}/{mm}/{dd}/{time}{rand:6}"", /* 上传保存路径,可以自定义保存路径和文件名格式 */
                              ""catcherUrlPrefix"": ""FileServerUrl"", /* 图片访问路径前缀 */
                              ""catcherMaxSize"": 2147483647, /* 上传大小限制，单位B */
                              ""catcherAllowFiles"": [ "".png"", "".jpg"", "".jpeg"", "".gif"", "".bmp"" ], /* 抓取图片格式显示 */
                            
                              /* 上传视频配置 */
                              ""videoActionName"": ""uploadvideo"", /* 执行上传视频的action名称 */
                              ""videoFieldName"": ""upfile"", /* 提交的视频表单名称 */
                              ""videoPathFormat"": ""/uploads/{yyyy}/{mm}/{dd}/{time}{rand:6}"", /* 上传保存路径,可以自定义保存路径和文件名格式 */
                              ""videoUrlPrefix"": ""FileServerUrl"", /* 视频访问路径前缀 */
                              ""videoMaxSize"": 2147483647, /* 上传大小限制，单位B，默认100MB */
                              ""videoAllowFiles"": ["".flv"","".swf"","".mkv"","".avi"","".rm"","".rmvb"","".mpeg"","".mpg"","".ogg"","".ogv"","".mov"","".wmv"","".mp4"","".webm"","".mp3"","".wav"","".mid""], /* 上传视频格式显示 */
                            
                              /* 上传文件配置 */
                              ""fileActionName"": ""uploadfile"", /* controller里,执行上传视频的action名称 */
                              ""fileFieldName"": ""upfile"", /* 提交的文件表单名称 */
                              ""filePathFormat"": ""/uploads/{yyyy}/{mm}/{dd}/{time}{rand:6}"", /* 上传保存路径,可以自定义保存路径和文件名格式 */
                              ""fileUrlPrefix"": ""FileServerUrl"", /* 文件访问路径前缀 */
                              ""fileMaxSize"": 2147483647, /* 上传大小限制，单位B，默认50MB */
                              ""fileAllowFiles"": [ "".png"", "".jpg"", "".jpeg"", "".gif"", "".bmp"", "".flv"", "".swf"", "".mkv"", "".avi"", "".rm"", "".rmvb"", "".mpeg"", "".mpg"", "".ogg"", "".ogv"", "".mov"", "".wmv"", "".mp4"", "".webm"", "".mp3"", "".wav"", "".mid"", "".rar"", "".zip"", "".tar"", "".gz"", "".7z"", "".bz2"", "".cab"", "".iso"", "".doc"", "".docx"", "".xls"", "".xlsx"", "".ppt"", "".pptx"", "".pdf"", "".txt"", "".md"", "".xml"" ] /* 上传文件格式显示 */
                            }";

            var fileServerUrl = Common.IO.Config.Get()["FileServerUrl"].ToString();

            json = json.Replace("FileServerUrl", fileServerUrl);

            var options = new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
            };

            return JsonDocument.Parse(json, options);
        }

        public static JsonDocument Items
        {
            get
            {
                if (noCache || _Items == null)
                {
                    _Items = BuildItems();
                }
                return _Items;
            }
        }
        private static JsonDocument? _Items;




        public static string[] GetStringList(string key)
        {
            return Items.RootElement.Clone().GetProperty(key).Deserialize<string[]>()!;
        }

        public static string GetString(string key)
        {
            return Items.RootElement.Clone().GetProperty(key).GetString()!;
        }

        public static int GetInt(string key)
        {
            return Items.RootElement.Clone().GetProperty(key).GetInt32();
        }
    }

}