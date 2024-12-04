namespace gateway.queue
{
	public class FileQueueItem
	{
		public Stream FileStream { get; }
		public string FileName { get; }

		public FileQueueItem(Stream fileStream, string fileName)
		{
			FileStream = fileStream;
			FileName = fileName;
		}
	}
}
