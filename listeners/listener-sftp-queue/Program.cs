using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Serilog;
using listener_sftp_queue;

Log.Logger = new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger();

try
{
	Log.Information("Запуск приложения");

	var host = Host.CreateDefaultBuilder(args)
		.UseSerilog() // Подключение Serilog
		.ConfigureServices((context, services) =>
		{
			// Настройка зависимостей
			services.AddSingleton<IConnectionFactory>(provider =>
				new ConnectionFactory
				{
					HostName = "localhost",
					UserName = "service",
					Password = "A1qwert"
				});

			services.AddSingleton<RabbitMqSftpListener>();
		})
		.Build();

	await host.RunAsync();
}
catch (Exception ex)
{
	Log.Fatal(ex, "Приложение завершилось с ошибкой");
}
finally
{
	Log.CloseAndFlush();
}
