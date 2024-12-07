using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi.Models;
using queue;
using RabbitMQ.Client;
using Serilog;
using ILogger = Serilog.ILogger;
using sftp_client;
using gateway.background;
using gateway.queue;

Console.Title = "gateway";

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// Настройка Serilog
builder.Host.UseSerilog((context, services, configuration) =>
	configuration.WriteTo.Console());

// Добавляем политику CORS
services.AddCors(options =>
{
	options.AddPolicy("AllowLocalhost",
		builder =>
		{
			//для возможности подгрузки из внешней страницы html
			builder.WithOrigins("http://127.0.0.1:5501")
				   .AllowAnyHeader()
				   .AllowAnyMethod();
		});
});


services.AddAntiforgery(); // Добавляем антифальсификацию (анти-CSRF)
services.AddHttpClient();
services.AddSingleton<IConnectionFactory>(new ConnectionFactory { Uri = new Uri("amqp://localhost") });
services.AddSingleton<QueueService>();
 
// Регистрация ILogger для DI
services.AddSingleton<ILogger>(new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger());

var sftpConfig = new SftpConfig
{
	Host = AppSettings.Host,
	Port = AppSettings.Port,
	UserName = AppSettings.UserName,
	Password = AppSettings.Password,
	Source = AppSettings.Source
};
// регистрация сервисов
services.AddSingleton(sftpConfig); // регистрируем конфигурацию
services.AddTransient<IFileUploadService, FileUploadService>(); // загружаем на нод
services.AddTransient<IFileDownloadService, FileDownloadService>(); // скачиваем оттуда

services.AddSingleton<FileProcessingQueue>();
services.AddHostedService<FileUploadBackgroundService>();
services.AddHostedService<FileDownloadBackgroundService>();

builder.Services.AddEndpointsApiExplorer(); // Генерация API-описания
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "Gateway API",
		Version = "v1",
		Description = "API для загрузки файлов и управления очередями"
	});
});

// Добавляем аутентификацию с использованием cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.LoginPath = "/login"; // Путь для входа
		options.LogoutPath = "/logout"; // Путь для выхода
	});

// Добавляем авторизацию
builder.Services.AddAuthorization();

var app = builder.Build();

// Добавляем поддержку аутентификации и авторизации
app.UseAuthentication();
app.UseAuthorization();

// Добавляем антифальсификацию
app.UseAntiforgery();

// Включаем HTTPS редирект
app.UseHttpsRedirection();

// Включение CORS с использованием вашей политики "AllowLocalhost"
app.UseCors("AllowLocalhost");

// Включение Swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway API v1");
	options.RoutePrefix = string.Empty; // Делаем Swagger доступным по корневому пути
});

app.MapPost("/upload", async (
	IFormFile file,
	FileProcessingQueue fileQueue,
	ILogger logger) =>
{
	if (file == null || file.Length == 0)
	{
		logger.Error("Файл не был загружен или пустой.");
		return Results.BadRequest("Файл не был загружен.");
	}

	try
	{
		// для выравнивания потока: в случае, если входящий поток данных будет настолько интенсивным, что он не будет успевать считываться.
		// там под капотом ConcurrentQueue<FileQueueItem>
		await fileQueue.EnqueueFileAsync(
			file.OpenReadStream(),
			file.FileName,
			CancellationToken.None);

		logger.Information("Файл {FileName} добавлен в очередь на загрузку.", file.FileName);
		return Results.Ok(new { Message = "Файл добавлен в очередь на загрузку." });
	}
	catch (Exception ex)
	{
		logger.Error("Ошибка при добавлении файла в очередь: {Error}", ex.Message);
		return Results.StatusCode(500);
	}
})
	.DisableAntiforgery();


app.MapGet("/download", async (
	IFileDownloadService downloadService,
	ILogger logger,
	CancellationToken cancellationToken) =>
{
	try
	{
		// скачаем все файлы, которые там находятся
		logger.Information("Начинается процесс скачивания файлов с сервера SFTP.");

		await downloadService.DownloadFilesInMemoryAsync(cancellationToken);

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
