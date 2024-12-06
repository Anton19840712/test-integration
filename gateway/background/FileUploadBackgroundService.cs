using gateway.queue;
using sftp_client;

namespace gateway.background
{
	/// <summary>
	/// Этот сервис является примером прослушивания очереди с подгружаемыми в нее файлами.
	/// Когда в этой очереди появляется файл, он считывается с интервало в одну секунду
	/// данным процессом и передается на sftp server. FileProcessingQueue используется для небольших файлов.
	/// Так как данные подгружаются в память. Для более крупных файлов 
	/// предлагается использовать подход сохранения файла на диск или в базу.
	/// </summary>
	public class FileUploadBackgroundService : BackgroundService
	{
		private readonly FileProcessingQueue _fileQueue;
		private readonly IFileUploadService _uploadService;
		private readonly ILogger<FileUploadBackgroundService> _logger;

		public FileUploadBackgroundService(
			FileProcessingQueue fileQueue,
			IFileUploadService uploadService,
			ILogger<FileUploadBackgroundService> logger)
		{
			_fileQueue = fileQueue;
			_uploadService = uploadService;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				if (_fileQueue.TryDequeue(out var item) && item != null)
				{
					try
					{
						await _uploadService.UploadStreamAsync(item.FileStream, item.FileName, stoppingToken);
						_logger.LogInformation($"Файл {item.FileName} успешно загружен.");
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, $"Ошибка при загрузке файла {item.FileName}: {ex.Message}");
					}
					finally
					{
						await item.FileStream.DisposeAsync(); // Освобождаем поток после использования
					}
				}

				await Task.Delay(1000, stoppingToken); // Задержка между проверками очереди
			}
		}
	}
}
