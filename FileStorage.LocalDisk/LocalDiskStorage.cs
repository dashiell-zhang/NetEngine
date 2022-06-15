namespace FileStorage.LocalDisk
{


    public class LocalDiskStorage : IFileStorage
    {
        public bool FileDelete(string remotePath)
        {
            throw new NotImplementedException();
        }

        public bool FileDownload(string remotePath, string localPath)
        {
            throw new NotImplementedException();
        }

        public bool FileUpload(string localPath, string remotePath, string? fileName = null)
        {
            throw new NotImplementedException();
        }

        public string? GetFileTempUrl(string remotePath, TimeSpan expiry, string? fileName = null)
        {
            throw new NotImplementedException();
        }
    }
}
