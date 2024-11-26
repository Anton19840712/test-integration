using System.Text;
using RabbitMQ.Client;
using Serilog;

namespace queue
{
	public class QueueService
	{
		private readonly IConnectionFactory _connectionFactory;
		private readonly ILogger _logger;
		private readonly Dictionary<string, string> _queues = new()
		{
			{ "server1", "queue1" },
			{ "server2", "queue2" },
			{ "server3", "queue3" }
		};

		public QueueService(
			IConnectionFactory connectionFactory,
			ILogger logger)
		{
			_connectionFactory = connectionFactory;
			_logger = logger;
		}

		public bool QueueExists(string server) => _queues.ContainsKey(server);

		public void PublishMessage(string server, string messageContent)
		{
			if (!_queues.TryGetValue(server, out var queueName))
			{
				_logger.Warning("Попытка отправить сообщение на несуществующую очередь: {Server}", server);
				return;
			}

			var enrichedMessage = new
			{
				Id = Guid.NewGuid(),
				ServerTag = server,
				Content = messageContent
			};

			var message = System.Text.Json.JsonSerializer.Serialize(enrichedMessage);
			var body = Encoding.UTF8.GetBytes(message);

			using var connection = _connectionFactory.CreateConnection();
			using var channel = connection.CreateModel();

			// Убедимся, что очередь существует
			channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

			// Публикуем сообщение в очередь RabbitMQ
			channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);

			_logger.Information("Сообщение с GUID {Id} добавлено в очередь {Server}", enrichedMessage.Id, server);
		}
	}
}
