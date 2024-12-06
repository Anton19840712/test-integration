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

// ��������� Serilog
Log.Logger = new LoggerConfiguration().CreateLogger();
builder.Host.UseSerilog((context, services, configuration) => configuration.WriteTo.Console());

services.AddHttpClient();

// ��������� ����� ��� amqp:
// services.AddSingleton<IConnectionFactory>(new ConnectionFactory { Uri = new Uri("amqp://localhost") });

// �� ������ ����� ��, ������ ��������� �������� ��������:
builder.Services.AddSingleton<IConnectionFactory>(provider =>
	new ConnectionFactory
	{
		HostName = "localhost", // ������� ���� ��������
		UserName = "service",
		Password = "A1qwert"
	});

builder.Services.AddSingleton<RabbitMqSftpListener>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<RabbitMqSftpListener>());

// ��������� SFTP
var sftpConfig = new SftpConfig
{
	Host = AppSettings.Host,
	Port = AppSettings.Port,
	UserName = AppSettings.UserName,
	Password = AppSettings.Password,
	Source = AppSettings.Source
};

//��� �������� ����������
services.AddTransient<IFileDownloadService, FileDownloadService>();
services.AddSingleton(sftpConfig);

// ��������� ������������ Swagger
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "PM API",
		Version = "v1",
		Description = "API ��� ���������� ������ ����� SFTP."
	});
});

// ���������� �������������� - ����� ����������� ��� ���.
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

// ����������� Swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/swagger/v1/swagger.json", "PM API v1");
	options.RoutePrefix = "swagger"; // Swagger �������� �� /swagger
});

// ������ �������, ������� ��������� sftp-listener
//POST http://localhost:5000/listener/start
//{
//	"queueName": "file_queue",
//    "saveDirectory": "C:/Downloads",
//    "intervalInSeconds": 10
//}



// ������� �������������� �������, ������� ����� �������� ��������� ��� listener
// HTTP-������ ���������� listener-��
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

// API ��� ���������� ��������� sftp server, � ��� �� sftp-listener ��
app.MapGet("/download", async (
	IFileDownloadService downloadService,
	ILogger logger,
	CancellationToken cancellationToken) =>
{
	try
	{
		logger.Information("���������� ������� ���������� ������ � ������� SFTP.");

		// ����� ��������� � ����� �� ������� �����:"C:\Documents2"
		await downloadService.DownloadFilesAsync(cancellationToken);
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
