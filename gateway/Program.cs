using queue;
using RabbitMQ.Client;
using Serilog;
using ILogger = Serilog.ILogger;

Console.Title = "gateway";

var builder = WebApplication.CreateBuilder(args);

// ��������� Serilog
builder.Host.UseSerilog((context, services, configuration) =>
	configuration.WriteTo.Console());

// ��������� �������� CORS
builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowLocalhost",
		builder =>
		{
			builder.WithOrigins("http://127.0.0.1:5501")
				   .AllowAnyHeader()
				   .AllowAnyMethod();
		});
});

// ����������� HttpClient ��� DI
builder.Services.AddHttpClient();

// ����������� RabbitMQ ��� DI
builder.Services.AddSingleton<IConnectionFactory>(new ConnectionFactory { Uri = new Uri("amqp://localhost") });

// ����������� QueueService ��� DI
builder.Services.AddSingleton<QueueService>();

// ����������� ILogger ��� DI
builder.Services.AddSingleton<ILogger>(new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger());

var app = builder.Build();

app.UseHttpsRedirection();

// ��������� CORS � �������������� ����� �������� "AllowLocalhost"
app.UseCors("AllowLocalhost");

app.MapGet("/queue/test", () =>
{
	var logger = app.Services.GetRequiredService<ILogger>();
	logger.Information("�������� GET ������ ��� ������.");
	return Results.Ok("GET ������ �������� �������.");
});

app.MapPost("/queue", async (string server, HttpContext context) =>
{
	// �������� ����������� �� ���������� DI
	var logger = context.RequestServices.GetRequiredService<ILogger>();
	var queueService = context.RequestServices.GetRequiredService<QueueService>();

	// �������� ������� �������
	if (!queueService.QueueExists(server))
	{
		logger.Warning("������� ��������� ������ �� �������������� ������: {Server}", server);
		context.Response.StatusCode = 400;
		return Results.Problem("��������� ������ �� ����������.");
	}

	using var reader = new StreamReader(context.Request.Body);
	var modelContent = await reader.ReadToEndAsync();

	// ���������� ��������� � ������� RabbitMQ
	queueService.PublishMessage(server, modelContent);

	return Results.Ok(new { message = $"��������� ��������� � ������� {server}" });

});

app.Run();
