using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi.Models;
using RabbitMQ.Client;
using Serilog;
using sftp_client;
using ILogger = Serilog.ILogger;

Console.Title = "endpoint-sftp";

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Настройка Serilog
builder.Host.UseSerilog((context, services, configuration) =>
	configuration.WriteTo.Console());

services.AddHttpClient();
services.AddSingleton<IConnectionFactory>(new ConnectionFactory { Uri = new Uri("amqp://localhost") });

// Регистрация ILogger для DI
services.AddSingleton<ILogger>(new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger());

// Настройка SFTP
var sftpConfig = new SftpConfig
{
	Host = AppSettings.Host,
	Port = AppSettings.Port,
	UserName = AppSettings.UserName,
	Password = AppSettings.Password,
	Source = AppSettings.Source
};

services.AddSingleton(sftpConfig);
services.AddTransient<IFileDownloadService, FileDownloadService>();

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

// Добавление аутентификации
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

// Маршрут для скачивания файлов
app.MapGet("/download", async (
	IFileDownloadService downloadService,
	ILogger logger,
	CancellationToken cancellationToken) =>
{
	try
	{
		logger.Information("Начинается процесс скачивания файлов с сервера SFTP.");
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
