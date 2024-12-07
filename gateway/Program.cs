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

// ��������� Serilog
builder.Host.UseSerilog((context, services, configuration) =>
	configuration.WriteTo.Console());

// ��������� �������� CORS
services.AddCors(options =>
{
	options.AddPolicy("AllowLocalhost",
		builder =>
		{
			//��� ����������� ��������� �� ������� �������� html
			builder.WithOrigins("http://127.0.0.1:5501")
				   .AllowAnyHeader()
				   .AllowAnyMethod();
		});
});


services.AddAntiforgery(); // ��������� ����������������� (����-CSRF)
services.AddHttpClient();
services.AddSingleton<IConnectionFactory>(new ConnectionFactory { Uri = new Uri("amqp://localhost") });
services.AddSingleton<QueueService>();
 
// ����������� ILogger ��� DI
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
// ����������� ��������
services.AddSingleton(sftpConfig); // ������������ ������������
services.AddTransient<IFileUploadService, FileUploadService>(); // ��������� �� ���
services.AddTransient<IFileDownloadService, FileDownloadService>(); // ��������� ������

services.AddSingleton<FileProcessingQueue>();
services.AddHostedService<FileUploadBackgroundService>();
services.AddHostedService<FileDownloadBackgroundService>();

builder.Services.AddEndpointsApiExplorer(); // ��������� API-��������
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "Gateway API",
		Version = "v1",
		Description = "API ��� �������� ������ � ���������� ���������"
	});
});

// ��������� �������������� � �������������� cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.LoginPath = "/login"; // ���� ��� �����
		options.LogoutPath = "/logout"; // ���� ��� ������
	});

// ��������� �����������
builder.Services.AddAuthorization();

var app = builder.Build();

// ��������� ��������� �������������� � �����������
app.UseAuthentication();
app.UseAuthorization();

// ��������� �����������������
app.UseAntiforgery();

// �������� HTTPS ��������
app.UseHttpsRedirection();

// ��������� CORS � �������������� ����� �������� "AllowLocalhost"
app.UseCors("AllowLocalhost");

// ��������� Swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway API v1");
	options.RoutePrefix = string.Empty; // ������ Swagger ��������� �� ��������� ����
});

app.MapPost("/upload", async (
	IFormFile file,
	FileProcessingQueue fileQueue,
	ILogger logger) =>
{
	if (file == null || file.Length == 0)
	{
		logger.Error("���� �� ��� �������� ��� ������.");
		return Results.BadRequest("���� �� ��� ��������.");
	}

	try
	{
		// ��� ������������ ������: � ������, ���� �������� ����� ������ ����� ��������� �����������, ��� �� �� ����� �������� �����������.
		// ��� ��� ������� ConcurrentQueue<FileQueueItem>
		await fileQueue.EnqueueFileAsync(
			file.OpenReadStream(),
			file.FileName,
			CancellationToken.None);

		logger.Information("���� {FileName} �������� � ������� �� ��������.", file.FileName);
		return Results.Ok(new { Message = "���� �������� � ������� �� ��������." });
	}
	catch (Exception ex)
	{
		logger.Error("������ ��� ���������� ����� � �������: {Error}", ex.Message);
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
		// ������� ��� �����, ������� ��� ���������
		logger.Information("���������� ������� ���������� ������ � ������� SFTP.");

		await downloadService.DownloadFilesInMemoryAsync(cancellationToken);

		logger.Information("������� ���������� ������ �������� �������.");
		return Results.Ok(new { Message = "����� ������� �������." });
	}
	catch (Exception ex)
	{
		logger.Error(ex, "������ ��� ���������� ������ � ������� SFTP.");
		return Results.StatusCode(500);
	}
});

app.Run();
