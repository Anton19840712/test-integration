using endpoint_sftp.background;
using listener_sftp_queue;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using RabbitMQ.Client;
using Serilog;
using sftp_client;
using ILogger = Serilog.ILogger;

Console.Title = "endpoint-for-listener-management";

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Настройка Serilog
Log.Logger = new LoggerConfiguration().CreateLogger();
builder.Host.UseSerilog((context, services, configuration) => configuration.WriteTo.Console());

services.AddHttpClient();

// добавляем связь под amqp:
// services.AddSingleton<IConnectionFactory>(new ConnectionFactory { Uri = new Uri("amqp://localhost") });

// по смыслу такая же, только локальная тестовая вариация:
builder.Services.AddSingleton<IConnectionFactory>(provider =>
	new ConnectionFactory
	{
		HostName = "localhost", // Укажите ваше значение
		UserName = "service",
		Password = "A1qwert"
	});

builder.Services.AddSingleton<RabbitMqSftpListener>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<RabbitMqSftpListener>());

// Настройка SFTP
var sftpConfig = new SftpConfig
{
	Host = AppSettings.Host,
	Port = AppSettings.Port,
	UserName = AppSettings.UserName,
	Password = AppSettings.Password,
	Source = AppSettings.Source
};

//как вариация скачивания
services.AddTransient<IFileDownloadService, FileDownloadService>();
services.AddSingleton(sftpConfig);

// Генерация документации Swagger
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "PM API",
		Version = "v1",
		Description = "API для скачивания файлов через SFTP."
	});
});

// Добавление аутентификации - нужно попробовать без нее.
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.LoginPath = "/login";
		options.LogoutPath = "/logout";
	});

services.AddAuthorization();

var app = builder.Build();

// Middleware
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

// Подключение Swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/swagger/v1/swagger.json", "PM API v1");
	options.RoutePrefix = "swagger"; // Swagger доступен на /swagger
});

// пример запроса, который запускает sftp-listener
//POST http://localhost:5000/listener/start
//{
//	"queueName": "file_queue",
//    "saveDirectory": "C:/Downloads",
//    "intervalInSeconds": 10
//}



// базовые предполагаемые команды, которые будет способен выполнять наш listener
// HTTP-методы управления listener-ом
app.MapPost("/listener/start", (
	RabbitMqSftpListener listener,
	string queueName,
	string saveDirectory,
	int intervalInSeconds) =>
{
	listener.Start(queueName, saveDirectory, intervalInSeconds);
	return Results.Ok("Listener started.");
});

app.MapPost("/listener/stop", (RabbitMqSftpListener listener) =>
{
	listener.Stop();
	return Results.Ok("Listener stopped.");
});

app.MapGet("/listener/status", (RabbitMqSftpListener listener) =>
{
	return Results.Ok(new { IsRunning = listener.IsRunning });
});

// API для управления удаленным sftp server, а так же sftp-listener ом
app.MapGet("/download", async (
	IFileDownloadService downloadService,
	ILogger logger,
	CancellationToken cancellationToken) =>
{
	try
	{
		logger.Information("Начинается процесс скачивания файлов с сервера SFTP.");

		// будем скачивать в место на жестком диске:"C:\Documents2"
		await downloadService.DownloadFilesAsync(cancellationToken);
		logger.Information("Процесс скачивания файлов завершён успешно.");
		return Results.Ok(new { Message = "Файлы успешно скачаны." });
	}
	catch (Exception ex)
	{
		logger.Error(ex, "Ошибка при скачивании файлов с сервера SFTP.");
		return Results.StatusCode(500);
	}
});

app.Run();
