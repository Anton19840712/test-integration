namespace sftp_client
{
    public interface IFileUploadService
    {
        Task UploadFilesAsync(CancellationToken cancellationToken);
        Task UploadStreamAsync(Stream fileStream, string fileName, CancellationToken cancellationToken);
	}
}
