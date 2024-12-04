using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace sftp_client
{
    public class FileDownloadService : IFileDownloadService
    {
        private readonly SftpConfig _config;
        private readonly ILogger<FileDownloadService> _logger;

        // Инъекция SftpConfig и ILogger
        public FileDownloadService(SftpConfig config, ILogger<FileDownloadService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task DownloadFilesAsync(CancellationToken cancellationToken)
        {
            // для скачивания создаем клиент
            using (var client = new SftpClient(_config.Host, _config.Port, _config.UserName, _config.Password))
            {
                try
                {
					// к нему коннектимся:
					await client.ConnectAsync(cancellationToken);

                    // например, тестовый сервер rebex должен быть установлен, чтобы на него было возможно что-либо там скачать
                    _logger.LogInformation("Подключение к серверу выполнено для скачивания файлов.");

					// получаем список файлов:
					var files = client.ListDirectory(_config.Source);
                    foreach (var file in files)
                    {
                        if (!file.IsDirectory && !file.IsSymbolicLink)
                        {
							// шпилим/скачиваем это все в Documents2:
							var localFilePath = Path.Combine(@"C:\Documents2", file.Name);
                            using (var fileStream = File.Create(localFilePath))
                            {
                                client.DownloadFile(file.FullName, fileStream);
                            }
							// логируем это дело:
							_logger.LogInformation($"Файл {file.Name} успешно скачан и сохранен в {localFilePath}.");
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Ошибка при скачивании файлов: {e.Message}");
                }
                finally
                {
                    client.Disconnect();
                    _logger.LogInformation("Отключение от сервера выполнено после скачивания файлов.");
                }
            }
        }
		public async Task<List<(string fileName, string fileContent)>> DownloadFilesInMemoryAsync(CancellationToken cancellationToken)
		{
			var downloadedFiles = new List<(string FileName, string FileContent)>();

			using (var client = new SftpClient(_config.Host, _config.Port, _config.UserName, _config.Password))
			{
				try
				{
					await client.ConnectAsync(cancellationToken);
					_logger.LogInformation("Подключение к серверу выполнено для скачивания файлов.");

					var files = client.ListDirectory(_config.Source);

					foreach (var file in files)
					{
						if (!file.IsDirectory && !file.IsSymbolicLink)
						{
							// Скачиваем файл в память
							using (var memoryStream = new MemoryStream())
							{
								client.DownloadFile(file.FullName, memoryStream);
								memoryStream.Position = 0;

								using var reader = new StreamReader(memoryStream);
								var fileContent = await reader.ReadToEndAsync();

								downloadedFiles.Add((file.Name, fileContent));
							}

							_logger.LogInformation($"Файл {file.Name} успешно скачан.");
						}
					}
				}
				catch (Exception e)
				{
					_logger.LogError(e, $"Ошибка при скачивании файлов: {e.Message}");
				}
				finally
				{
					client.Disconnect();
					_logger.LogInformation("Отключение от сервера выполнено.");
				}
			}

			return downloadedFiles;
		}
	}
}
