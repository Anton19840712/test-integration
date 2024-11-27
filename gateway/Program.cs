using System.Text;
using queue;
using RabbitMQ.Client;
using Serilog;
using ILogger = Serilog.ILogger;

Console.Title = "gateway";

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog
builder.Host.UseSerilog((context, services, configuration) =>
	configuration.WriteTo.Console());

// Добавляем политику CORS
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

// Регистрация HttpClient для DI
builder.Services.AddHttpClient();

// Регистрация RabbitMQ для DI
builder.Services.AddSingleton<IConnectionFactory>(new ConnectionFactory { Uri = new Uri("amqp://localhost") });

// Регистрация QueueService для DI
builder.Services.AddSingleton<QueueService>();

// Регистрация ILogger для DI
builder.Services.AddSingleton<ILogger>(new LoggerConfiguration()
	.WriteTo.Console()
	.CreateLogger());

var app = builder.Build();

app.UseHttpsRedirection();

// Включение CORS с использованием вашей политики "AllowLocalhost"
app.UseCors("AllowLocalhost");

app.MapGet("/queue/test", () =>
{
	var logger = app.Services.GetRequiredService<ILogger>();
	logger.Information("Тестовый GET запрос был вызван.");
	return Results.Ok("GET запрос выполнен успешно.");
});

app.MapPost("/queue", async (string server, HttpContext context) =>
{
	// Получаем зависимости из контейнера DI
	var logger = context.RequestServices.GetRequiredService<ILogger>();
	var queueService = context.RequestServices.GetRequiredService<QueueService>();

	// Проверка наличия очереди
	if (!queueService.QueueExists(server))
	{
		logger.Warning("Попытка отправить модель на несуществующий сервер: {Server}", server);
		context.Response.StatusCode = 400;
		return Results.Problem("Указанный сервер не существует.");
	}

	using var reader = new StreamReader(context.Request.Body);
	var modelContent = await reader.ReadToEndAsync();

	// Публикация сообщения в очередь RabbitMQ
	queueService.PublishMessage(server, modelContent);

	// Отправка SOAP ответа
	string payload = "<?xml version='1.0' encoding='utf-8'?>\r\n" +
					 "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
					 "<soapenv:Body>\r\n" +
					 "<card112ChangedResponse xmlns=\"http://www.protei.ru/emergency/integration\">\r\n" +
					 "<errorCode>0</errorCode>\r\n" +
					 "<errorMessage></errorMessage>\r\n" +
					 "</card112ChangedResponse>\r\n" +
					 "</soapenv:Body></soapenv:Envelope>";

	await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(payload));
	logger.Information("Sent SOAP response: {Response}", payload);

	return Results.Empty; // Возвращаем пустой результат, так как ответ уже отправлен в context.Response.Body
});

app.Run();
