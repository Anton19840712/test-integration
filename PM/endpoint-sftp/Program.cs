using listener_sftp_queue;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using RabbitMQ.Client;
using Serilog;

Console.Title = "endpoint-for-listener-management";

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

// ��������� Serilog
Log.Logger = new LoggerConfiguration().CreateLogger();
builder.Host.UseSerilog((context, services, configuration) => configuration.WriteTo.Console());

services.AddHttpClient();

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

// ���������� ��������������
services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
	.AddCookie(options =>
	{
		options.LoginPath = "/login";
		options.LogoutPath = "/logout";
	});

services.AddAuthorization();
services.AddSingleton<IConnectionFactory>(provider =>
				new ConnectionFactory
				{
					HostName = "localhost",
					UserName = "service",
					Password = "A1qwert"
				});

services.AddSingleton<RabbitMqSftpListener>(); // ��������� Listener ��� ������

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
// POST http://localhost:5000/listener/start
// {
//     "queueName": "file_queue",
//     "saveDirectory": "C:/Downloads",
//     "intervalInSeconds": 10
// }

// HTTP-������ ��� ���������� listener'��
app.MapPost("/listener/start", (
	[FromServices] RabbitMqSftpListener listener,
	[FromBody] ListenerStartRequest request) =>
{
	listener.Start(request.QueueName, request.SaveDirectory, request.IntervalInSeconds);
	return Results.Ok("Listener started.");
});

app.MapPost("/listener/stop", ([FromServices] RabbitMqSftpListener listener) =>
{
	listener.Stop();
	return Results.Ok("Listener stopped.");
});

app.MapGet("/listener/status", ([FromServices] RabbitMqSftpListener listener) =>
{
	return Results.Ok(new { IsRunning = listener.IsRunning });
});


app.Run();

// ������ �������� ��� ������ � ��������� listener'�
public class ListenerStartRequest
{
	public string QueueName { get; set; }
	public string SaveDirectory { get; set; }
	public int IntervalInSeconds { get; set; }
}
