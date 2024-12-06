using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;

namespace listener_sftp_queue
{
	public class RabbitMqSftpListener : IHostedService
	{
		private readonly IConnectionFactory _connectionFactory;
		private IConnection _connection;
		private IModel _channel;
		private CancellationTokenSource _cts;
		private Task _listenerTask;
		private readonly ILogger _logger = Log.ForContext<RabbitMqSftpListener>();

		private string _queueName;
		private string _saveDirectory;
		private int _intervalInSeconds;

		private bool _isRunning;

		public RabbitMqSftpListener(IConnectionFactory connectionFactory)
		{
			_connectionFactory = connectionFactory;
		}

		public Task StartAsync(CancellationToken cancellationToken)
		{
			// Этот метод запускается при старте приложения, по сути, он носит фейковый характер
			_logger.Information("RabbitMqSftpListener initialized.");
			return Task.CompletedTask;
		}

		public Task StopAsync(CancellationToken cancellationToken)
		{
			// Этот метод вызывается при остановке приложения
			Stop();
			return Task.CompletedTask;
		}

		public void Start(string queueName, string saveDirectory, int intervalInSeconds)
		{
			if (_isRunning)
			{
				_logger.Warning("Listener is already running.");
				return;
			}

			_queueName = queueName;
			_saveDirectory = saveDirectory;
			_intervalInSeconds = intervalInSeconds;

			_logger.Information("Starting listener with queue: {QueueName}", queueName);

			_cts = new CancellationTokenSource();
			_listenerTask = Task.Run(async () =>
			{
				_connection = _connectionFactory.CreateConnection();
				_channel = _connection.CreateModel();

				_channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

				var consumer = new EventingBasicConsumer(_channel);
				consumer.Received += (model, ea) =>
				{
					var body = ea.Body.ToArray();
					var fileName = $"{Guid.NewGuid()}.bin";
					var filePath = Path.Combine(saveDirectory, fileName);

					if (!Directory.Exists(saveDirectory))
					{
						Directory.CreateDirectory(saveDirectory);
					}

					File.WriteAllBytes(filePath, body);
					_logger.Information("File saved: {FilePath}", filePath);

					_channel.BasicAck(ea.DeliveryTag, false);
				};

				_channel.BasicConsume(queueName, false, consumer);

				while (!_cts.Token.IsCancellationRequested)
				{
					await Task.Delay(intervalInSeconds * 1000, _cts.Token);
				}
			}, _cts.Token);

			_isRunning = true;
		}

		public void Stop()
		{
			if (!_isRunning)
			{
				_logger.Warning("Listener is not running.");
				return;
			}

			_cts.Cancel();
			_listenerTask?.Wait();

			_channel?.Close();
			_connection?.Close();

			_logger.Information("Listener stopped.");
			_isRunning = false;
		}

		public bool IsRunning => _isRunning;
	}
}
