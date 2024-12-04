using Serilog;
using ILogger = Serilog.ILogger;


Console.Title = "endpoint1";

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog

builder.Host.UseSerilog((ctx, cfg) => cfg
				   .ReadFrom.Configuration(ctx.Configuration)
				   .WriteTo.Console()
				   .WriteTo.Seq("http://localhost:5341")
			   );

var app = builder.Build();

app.UseHttpsRedirection();

// Endpoint для получения модели
app.MapPost("/status1", async (HttpContext context, ILogger logger) =>
{
	// Чтение содержимого запроса
	using var reader = new StreamReader(context.Request.Body);
	var modelContent = await reader.ReadToEndAsync();

	// Логируем полученное сообщение
	logger.Information("Received message: {ModelContent}", modelContent);

	// В случае, если модель пустая или некорректная, логируем ошибку
	if (string.IsNullOrEmpty(modelContent))
	{
		logger.Warning("Received an empty or invalid model.");
		return Results.BadRequest("Received an empty or invalid model.");
	}

	// Возвращаем успешный ответ и логируем его
	logger.Information("Successfully processed the model.");
	return Results.Ok("Received and logged the model.");
});

app.Run();
