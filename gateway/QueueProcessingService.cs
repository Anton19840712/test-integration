using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ILogger = Serilog.ILogger;
namespace gateway
{
	public class QueueProcessingService : BackgroundService
	{
		private readonly ILogger _logger;
		private readonly IConnectionFactory _connectionFactory;

		public QueueProcessingService(
			IConnectionFactory connectionFactory,
			ILogger logger)
		{
			_connectionFactory = connectionFactory;
			_logger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// Создаем подключение к RabbitMQ
			using var connection = _connectionFactory.CreateConnection();
			using var channel = connection.CreateModel();
			var queues = new Dictionary<string, string>
		{
			{ "server1", "queue1" },
			{ "server2", "queue2" },
			{ "server3", "queue3" }
		};

			// Подписываемся на очереди для всех серверов
			foreach (var queue in queues.Values)
			{
				channel.QueueDeclare(
					queue,
					durable: true,
					exclusive: false,
					autoDelete: false,
					arguments: null);

				// Создаем Consumer
				var consumer = new EventingBasicConsumer(channel);
				consumer.Received += (model, ea) =>
				{
					var body = ea.Body.ToArray();
					var message = Encoding.UTF8.GetString(body);
					var receivedMessage = System.Text.Json.JsonSerializer.Deserialize<dynamic>(message);

					_logger.Information(
						"Получено сообщение с GUID {Id} для сервера {Server}",
						receivedMessage?.Id,
						receivedMessage?.ServerTag);

					// Здесь можно добавить логику обработки сообщения
					// Например, отправить его на нужный API сервер или выполнить другую задачу
				};

				// Подключаем consumer к очереди
				channel.BasicConsume(queue, autoAck: true, consumer: consumer);
			}

			// Не завершать процесс до получения сигнала остановки
			await Task.CompletedTask;
		}
	}
}
