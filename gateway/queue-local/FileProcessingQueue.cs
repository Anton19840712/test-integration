using System.Collections.Concurrent;
using System.Threading.Channels;

namespace gateway.queue
{
	public class FileProcessingQueue
	{
		private readonly ConcurrentQueue<FileQueueItem> _queue = new();

		public Task EnqueueFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken)
		{
			var memoryStream = new MemoryStream();
			fileStream.CopyTo(memoryStream);
			memoryStream.Position = 0; // Возвращаем указатель потока на начало

			var item = new FileQueueItem(memoryStream, fileName);
			_queue.Enqueue(item);
			return Task.CompletedTask;
		}

		public bool TryDequeue(out FileQueueItem item)
		{
			return _queue.TryDequeue(out item);
		}
	}
}
