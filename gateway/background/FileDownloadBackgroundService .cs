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

		/// <summary>
		/// Данный метод запускается, когда стартует FileDownloadBackgroundService непосредственно.
		/// Сразу же при запуске он идет на sftp сервер и скачивает оттуда все существующие файлы. 
		/// Настроен таким образом, чтобы удерживать task на каждые 5 минут.
		/// </summary>
		/// <param name="stoppingToken"></param>
		/// <returns></returns>
		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			_logger.LogInformation("Сервис фоновой загрузки файлов запущен.");

			while (!stoppingToken.IsCancellationRequested)
			{
				try
				{
					_logger.LogInformation("Начинается загрузка файлов с SFTP-сервера.");
					var downloadedFiles = await _fileDownloadService.DownloadFilesInMemoryAsync(stoppingToken);

					foreach (var (fileName, fileContent, fileExtension) in downloadedFiles)
					{
						_logger.LogInformation("Публикация файла {FileName} в очередь.", fileName);

						// когда ты скачиваешь с сервера данные, то именно и только здесь ты их скачиваешь сначала в сетевую очередь,
						// из которой будет прослушивать твой sftp listener
						_queueService.PublishMessage("sftp", fileContent, fileExtension);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Ошибка в процессе фоновой загрузки файлов.");
				}

				// Ожидание перед следующим циклом обращения background service за данными на sftp server:
				await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
			}
		}
	}
}
