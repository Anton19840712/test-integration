using queue;
using sftp_client;

namespace gateway.background
{
	public class FileDownloadBackgroundService : BackgroundService
	{
		private readonly IFileDownloadService _fileDownloadService;
		private readonly QueueService _queueService;
		private readonly ILogger<FileDownloadBackgroundService> _logger;

		public FileDownloadBackgroundService(
			IFileDownloadService fileDownloadService,
			QueueService queueService,
			ILogger<FileDownloadBackgroundService> logger)
		{
			_fileDownloadService = fileDownloadService;
			_queueService = queueService;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("Сервис фоновой загрузки файлов запущен.");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					_logger.LogInformation("Начинается загрузка файлов с SFTP-сервера.");
					var downloadedFiles = await _fileDownloadService.DownloadFilesInMemoryAsync(stoppingToken);

					foreach (var (fileName, fileContent) in downloadedFiles)
					{
						_logger.LogInformation("Публикация файла {FileName} в очередь.", fileName);
						_queueService.PublishMessage("sftp", fileContent);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Ошибка в процессе фоновой загрузки файлов.");
				}

				// Ожидание перед следующим циклом
				await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
			}
		}

	}
}
