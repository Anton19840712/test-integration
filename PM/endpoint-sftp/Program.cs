using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.OpenApi.Models;
using RabbitMQ.Client;
using Serilog;
using sftp_client;
using ILogger = Serilog.ILogger;

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
			builder.WithOrigins("http://127.0.0.1:5501")
				   .AllowAnyHeader()
				   .AllowAnyMethod();
		});
});


services.AddAntiforgery(); // ��������� ����������������� (����-CSRF)
services.AddHttpClient();
services.AddSingleton<RabbitMQ.Client.IConnectionFactory>(new ConnectionFactory { Uri = new Uri("amqp://localhost") });

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
services.AddTransient<IFileDownloadService, FileDownloadService>();

builder.Services.AddEndpointsApiExplorer(); // ��������� API-��������
builder.Services.AddSwaggerGen(options =>
{
	options.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "PM API",
		Version = "v1",
		Description = "API PM"
	});
});

// ��������� �������������� � �������������� cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.LoginPath = "/login"; // ���� ��� �����
		options.LogoutPath = "/logout"; // ���� ��� ������
	});

builder.Services.AddAuthorization();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();
app.UseCors("AllowLocalhost");

// ��������� Swagger
app.UseSwagger();
app.UseSwaggerUI(options =>
{
	options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gateway API v1");
	options.RoutePrefix = string.Empty; // ������ Swagger ��������� �� ��������� ����
});


app.MapGet("/download", async (
	IFileDownloadService downloadService,
	ILogger logger,
	CancellationToken cancellationToken) =>
{
	try
	{
		// ������� ��� �����, ������� ��� ���������
		logger.Information("���������� ������� ���������� ������ � ������� SFTP.");

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
