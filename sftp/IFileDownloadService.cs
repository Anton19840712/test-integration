namespace sftp_client
{
    public interface IFileDownloadService
    {
        Task DownloadFilesAsync(CancellationToken cancellationToken);
        Task<List<(string fileName, byte[] fileContent, string fileExtension)>> DownloadFilesInMemoryAsync(CancellationToken cancellationToken);
	}
}
