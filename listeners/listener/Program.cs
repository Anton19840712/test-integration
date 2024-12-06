using Microsoft.Extensions.Hosting;
using RabbitMQ.Client.Events;
using RabbitMQ.Client;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using System.Xml;
using Newtonsoft.Json;

public class Program
{
	public static void Main(string[] args)
	{
		Console.Title = "htttp-listener";

		// Настройка Serilog
		Log.Logger = new LoggerConfiguration()
			.WriteTo.Console()
			.CreateLogger();

		try
		{
			Log.Information("Starting RabbitMQ Listener Host...");

			// Создание и запуск хоста
			CreateHostBuilder(args).Build().Run();
		}
		catch (Exception ex)
		{
			Log.Fatal(ex, "Host terminated unexpectedly");
		}
		finally
		{
			Log.CloseAndFlush();
		}
	}

	public static IHostBuilder CreateHostBuilder(string[] args) =>
		Host.CreateDefaultBuilder(args)
			.UseSerilog() // Подключаем Serilog к хосту
			.ConfigureServices((hostContext, services) =>
			{
				services.AddSingleton<IConnectionFactory>(provider =>
				   new ConnectionFactory
				   {
					   HostName = "localhost", // Укажите ваше значение, если отличается
					   UserName = "service",     // Имя пользователя
					   Password = "A1qwert"      // Пароль
				   });

				services.AddHttpClient();
				services.AddHostedService<RabbitMqListenerService>();
			});


	//public static IHostBuilder CreateHostBuilder(string[] args) =>
	//	Host.CreateDefaultBuilder(args)
	//		.UseSerilog() // Подключаем Serilog к хосту
	//		.ConfigureServices((hostContext, services) =>
	//		{
	//			// Прямо регистрируем IConnectionFactory с настройками
	//			services.AddSingleton<IConnectionFactory>(new ConnectionFactory
	//			{
	//				Uri = new Uri("amqp://admin:admin@172.16.211.18/termidesk") // Ваш адрес RabbitMQ
	//			});

	//			services.AddHttpClient();
	//			services.AddHostedService<RabbitMqListenerService>();
	//		});
}

	// Фоновый сервис для прослушивания очереди RabbitMQ
	public class RabbitMqListenerService : BackgroundService
	{
		private readonly ILogger _logger;
		private readonly IConnectionFactory _connectionFactory;
		private readonly IHttpClientFactory _httpClientFactory;
		private IConnection _connection;
		private IModel _channel;

		public RabbitMqListenerService(
			IConnectionFactory connectionFactory,
			IHttpClientFactory httpClientFactory)
		{
			_logger = Log.ForContext<RabbitMqListenerService>();
			_connectionFactory = connectionFactory;
			_httpClientFactory = httpClientFactory;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		stoppingToken.Register(() =>
			_logger.Information("RabbitMQ Listener is stopping."));

		// Создаем подключение и канал для прослушивания
		_connection = _connectionFactory.CreateConnection();
		_channel = _connection.CreateModel();

		// Устанавливаем очереди для разных серверов
		var queues = new[] { "queue1", "queue2", "queue3" };

		// Словарь, который сопоставляет очередь с сервером
		var queueToUrlMapping = new Dictionary<string, string>
	{
		{ "queue1", "https://localhost:7270/status1" },
		{ "queue2", "https://localhost:7212/status1" },
		{ "queue3", "https://localhost:7077/status1" }
	};

		// Настроим очереди и начнем слушать каждую
		foreach (var queue in queues)
		{
			_channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false, arguments: null);

			var consumer = new EventingBasicConsumer(_channel);
			consumer.Received += async (model, ea) =>
			{
				var body = ea.Body.ToArray();
				var message = Encoding.UTF8.GetString(body);

				// Логируем исходное сообщение
				_logger.Information("Received raw message from {QueueName}: {Message}", queue, message);

				// Попытаться определить, является ли сообщение JSON или XML
				object parsedMessage = null;
				string formattedMessage = string.Empty;


				// Проверяем, является ли сообщение JSON
				try
				{
					parsedMessage = JsonConvert.DeserializeObject(message);
					formattedMessage = JsonConvert.SerializeObject(parsedMessage, Newtonsoft.Json.Formatting.Indented);
				}
				catch (JsonException)
				{
					// Если это не JSON, пытаемся обработать как XML
					try
					{
						var xmlDoc = new XmlDocument();
						xmlDoc.LoadXml(message);
						formattedMessage = xmlDoc.OuterXml; // Получаем отформатированный XML
					}
					catch (XmlException)
					{
						_logger.Error("Received message is neither valid JSON nor XML.");
						return; // Прерываем, если формат неизвестен
					}
				}

				// Логируем отформатированное сообщение
				_logger.Information("Formatted message: {Message}", formattedMessage);

				// Получаем соответствующий URL для текущей очереди
				if (queueToUrlMapping.TryGetValue(queue, out var url))
				{
					// Отправляем сообщение на соответствующий сервер
					var httpClient = _httpClientFactory.CreateClient();
					var content = new StringContent(formattedMessage, Encoding.UTF8, "application/json");

					try
					{
						var response = await httpClient.PostAsync(url, content);

						if (response.IsSuccessStatusCode)
						{
							_logger.Information("Message successfully sent to {Url}.", url);
						}
						else
						{
							_logger.Error(
								"Failed to send message to {Url}. StatusCode: {StatusCode}",
								url, response.StatusCode);
						}
					}
					catch (Exception ex)
					{
						_logger.Error(ex, "Error sending message to {Url}.", url);
					}
				}
				else
				{
					_logger.Warning("No URL mapping found for queue: {QueueName}", queue);
				}

				// Подтверждаем обработку сообщения
				_channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
			};

			// Подключаем consumer к каждой очереди
			_channel.BasicConsume(queue, autoAck: false, consumer: consumer);

			_logger.Information("Listening on queue: {QueueName}", queue);
		}

		await Task.CompletedTask;
	}

	// Закрываем подключение и канал при остановке сервиса
	public override void Dispose()
	{
		_channel?.Dispose();
		_connection?.Dispose();
		base.Dispose();
	}
}

