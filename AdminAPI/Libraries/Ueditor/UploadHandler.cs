using FileStorage;



namespace AdminAPI.Libraries.Ueditor
{
    /// <summary>
    /// UploadHandler 的摘要说明
    /// </summary>
    public class UploadHandler : Handler
    {

        public UploadConfig UploadConfig { get; private set; }
        public UploadResult Result { get; private set; }

        private readonly string rootPath;

        private readonly HttpContext httpContext;

        private readonly IFileStorage? fileStorage;


        public UploadHandler(UploadConfig config, string rootPath, HttpContext httpContext) : base()
        {
            UploadConfig = config;
            Result = new UploadResult() { State = UploadState.Unknown };

            this.rootPath = rootPath;
            this.httpContext = httpContext;

            fileStorage = httpContext.RequestServices.GetService<IFileStorage>();
        }

        public override string Process(string fileServerUrl)
        {

            string value = "";
            string uploadFileName;
            if (UploadConfig.Base64)
            {
                uploadFileName = UploadConfig.Base64Filename!;
                byte[] uploadFileBytes = Convert.FromBase64String(httpContext.Current().Request.Form[UploadConfig.UploadFieldName!]!);

                var savePath = PathFormatter.Format(uploadFileName, UploadConfig.PathFormat!);
                var localPath = Path.Combine(rootPath, savePath);

                try
                {

                    if (!Directory.Exists(Path.GetDirectoryName(localPath)))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                    }
                    File.WriteAllBytes(localPath, uploadFileBytes);


                    var utcNow = DateTime.UtcNow;


                    if (fileStorage != null)
                    {
                        string basePath = Path.Combine("uploads", utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

                        var upload = fileStorage.FileUpload(localPath, basePath, Path.GetFileName(localPath));

                        if (upload)
                        {
                            Common.IOHelper.DeleteFile(localPath);

                            Result.Url = Path.Combine(basePath, Path.GetFileName(localPath)).Replace("\\", "/");
                            Result.State = UploadState.Success;
                        }
                        else
                        {
                            Result.State = UploadState.FileAccessError;
                            Result.ErrorMessage = "文件存储转存失败";
                        }
                    }
                    else
                    {
                        Result.Url = savePath;
                        Result.State = UploadState.Success;
                    }

                }
                catch (Exception e)
                {
                    Result.State = UploadState.FileAccessError;
                    Result.ErrorMessage = e.Message;
                }
                finally
                {
                    value = WriteResult();
                }
            }
            else
            {
                var file = httpContext.Current().Request.Form.Files[UploadConfig.UploadFieldName!]!;
                uploadFileName = file.FileName;

                if (!CheckFileType(uploadFileName))
                {
                    Result.State = UploadState.TypeNotAllow;
                    value = WriteResult();
                }

                int filelength = Convert.ToInt32(file.Length);

                if (!CheckFileSize(filelength))
                {
                    Result.State = UploadState.SizeLimitExceed;
                    value = WriteResult();
                }

                _ = new byte[file.Length];
                try
                {
                    file.OpenReadStream();

                    string savePath = PathFormatter.Format(uploadFileName, UploadConfig.PathFormat!);
                    string localPath = Path.Combine(rootPath, savePath);

                    try
                    {



                        if (!Directory.Exists(Path.GetDirectoryName(localPath)))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
                        }
                        using (FileStream fs = System.IO.File.Create(localPath))
                        {
                            file.CopyTo(fs);
                            fs.Flush();
                        }


                        if (fileStorage != null)
                        {
                            var utcNow = DateTime.UtcNow;

                            string basePath = Path.Combine("uploads", utcNow.ToString("yyyy"), utcNow.ToString("MM"), utcNow.ToString("dd"));

                            var upload = fileStorage.FileUpload(localPath, basePath, file.FileName);

                            if (upload)
                            {
                                Common.IOHelper.DeleteFile(localPath);

                                Result.Url = Path.Combine(basePath, Path.GetFileName(localPath)).Replace("\\", "/");
                                Result.State = UploadState.Success;
                            }
                            else
                            {
                                Result.State = UploadState.FileAccessError;
                                Result.ErrorMessage = "文件存储转存失败";
                            }
                        }
                        else
                        {
                            Result.Url = savePath;
                            Result.State = UploadState.Success;
                        }

                    }
                    catch (Exception e)
                    {
                        Result.State = UploadState.FileAccessError;
                        Result.ErrorMessage = e.Message;
                    }
                    finally
                    {
                        value = WriteResult();
                    }

                }
                catch (Exception)
                {
                    Result.State = UploadState.NetworkError;
                    WriteResult();
                }
            }




            return value;
        }

        private string WriteResult()
        {
            return this.WriteJson(new
            {
                state = GetStateMessage(Result.State),
                url = Result.Url,
                title = Result.OriginFileName,
                original = Result.OriginFileName,
                error = Result.ErrorMessage
            });
        }

        private static string GetStateMessage(UploadState state)
        {
            return state switch
            {
                UploadState.Success => "SUCCESS",
                UploadState.FileAccessError => "文件访问出错，请检查写入权限",
                UploadState.SizeLimitExceed => "文件大小超出服务器限制",
                UploadState.TypeNotAllow => "不允许的文件格式",
                UploadState.NetworkError => "网络错误",
                _ => "未知错误",
            };
        }

        private bool CheckFileType(string filename)
        {
            var fileExtension = Path.GetExtension(filename).ToLower();
            return UploadConfig.AllowExtensions!.Select(x => x.ToLower()).Contains(fileExtension);
        }

        private bool CheckFileSize(int size)
        {
            return size < UploadConfig.SizeLimit;
        }
    }

    public class UploadConfig
    {
        /// <summary>
        /// 文件命名规则
        /// </summary>
        public string? PathFormat { get; set; }

        /// <summary>
        /// 上传表单域名称
        /// </summary>
        public string? UploadFieldName { get; set; }

        /// <summary>
        /// 上传大小限制
        /// </summary>
        public int SizeLimit { get; set; }

        /// <summary>
        /// 上传允许的文件格式
        /// </summary>
        public string[]? AllowExtensions { get; set; }

        /// <summary>
        /// 文件是否以 Base64 的形式上传
        /// </summary>
        public bool Base64 { get; set; }

        /// <summary>
        /// Base64 字符串所表示的文件名
        /// </summary>
        public string? Base64Filename { get; set; }
    }

    public class UploadResult
    {
        public UploadState State { get; set; }
        public string? Url { get; set; }
        public string? OriginFileName { get; set; }

        public string? ErrorMessage { get; set; }
    }

    public enum UploadState
    {
        Success = 0,
        SizeLimitExceed = -1,
        TypeNotAllow = -2,
        FileAccessError = -3,
        NetworkError = -4,
        Unknown = 1,
    }

}