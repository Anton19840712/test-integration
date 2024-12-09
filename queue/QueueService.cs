using System.Text;
using System.Xml;
using Newtonsoft.Json;
using RabbitMQ.Client;
using Serilog;

namespace queue
{
	public class QueueService
	{
		private readonly IConnectionFactory _connectionFactory;
		private readonly ILogger _logger;

		public QueueService(
			IConnectionFactory connectionFactory,
			ILogger logger)
		{
			_connectionFactory = connectionFactory;
			_logger = logger;
		}
		public void PublishMessage(string server, string messageContent)
		{
			var queueName = server;  // Используем значение сервера как имя очереди

			// Проверка, является ли сообщение XML
			if (IsXml(messageContent))
			{
				messageContent = ConvertXmlToJson(messageContent); // Преобразуем XML в JSON
				_logger.Information("Сообщение было преобразовано из XML в JSON.");
			}

			var enrichedMessage = new
			{
				Id = Guid.NewGuid(),
				ServerTag = server,
				Content = messageContent
			};

			var message = System.Text.Json.JsonSerializer.Serialize(enrichedMessage);
			var body = Encoding.UTF8.GetBytes(message);

			PublishToQueue(queueName, body, null);
		}

		public void PublishMessage(string server, byte[] fileContent, string fileExtension)
		{
			var queueName = server;  // Используем значение сервера как имя очереди

			PublishToQueue(queueName, fileContent, fileExtension);
		}

		/// <summary>
		/// Метод отправляет сообщение, передаваемое в байтах.
		/// </summary>
		/// <param name="queueName"></param>
		/// <param name="body"></param>
		private void PublishToQueue(string queueName, byte[] body, string fileExtension)
		{
			using var connection = _connectionFactory.CreateConnection();
			using var channel = connection.CreateModel();

			// Убедимся, что очередь существует
			channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

			// Создание заголовков сообщения
			var properties = channel.CreateBasicProperties();
			properties.Headers = new Dictionary<string, object>
			{
				{ "fileExtension", fileExtension } // передаем расширение файла
			};

			// Публикуем сообщение в очередь RabbitMQ
			channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: properties, body: body);

			_logger.Information("Сообщение добавлено в очередь {Server}", queueName);
		}

		private bool IsXml(string content)
		{
			// Проверка на начало XML документа
			return content.TrimStart().StartsWith("<");
		}

		private string ConvertXmlToJson(string xml)
		{
			var xmlDoc = new XmlDocument();
			xmlDoc.LoadXml(xml);
			var jsonText = JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);

			return jsonText;
		}
	}
}
