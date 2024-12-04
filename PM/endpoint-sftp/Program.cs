using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi.Models;
using RabbitMQ.Client;
using Serilog;
using sftp_client;
using ILogger = Serilog.ILogger;

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
			builder.WithOrigins("http://127.0.0.1:5501")
				   .AllowAnyHeader()
				   .AllowAnyMethod();
		});
});


services.AddAntiforgery(); // Добавляем антифальсификацию (анти-CSRF)
services.AddHttpClient();
services.AddSingleton<RabbitMQ.Client.IConnectionFactory>(new ConnectionFactory { Uri = new Uri("amqp://localhost") });

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
services.AddTransient<IFileDownloadService, FileDownloadService>();

builder.Services.AddEndpointsApiExplorer(); // Генерация API-описания
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "PM API",
		Version = "v1",
		Description = "API PM"
	});
});

// Добавляем аутентификацию с использованием cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.LoginPath = "/login"; // Путь для входа
		options.LogoutPath = "/logout"; // Путь для выхода
	});

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();
app.UseCors("AllowLocalhost");

// Включение Swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway API v1");
	options.RoutePrefix = string.Empty; // Делаем Swagger доступным по корневому пути
});


app.MapGet("/download", async (
	IFileDownloadService downloadService,
	ILogger logger,
	CancellationToken cancellationToken) =>
{
	try
	{
		// скачаем все файлы, которые там находятся
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
