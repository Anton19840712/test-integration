namespace sftp_client
{
    public interface IFileDownloadService
    {
        Task DownloadFilesAsync(CancellationToken cancellationToken);
        Task<List<(string fileName, string fileContent)>> DownloadFilesInMemoryAsync(CancellationToken cancellationToken);
	}
}
