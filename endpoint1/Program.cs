using Serilog;
using ILogger = Serilog.ILogger;


Console.Title = "endpoint1";

var builder = WebApplication.CreateBuilder(args);

// ��������� Serilog

builder.Host.UseSerilog((ctx, cfg) => cfg
				   .ReadFrom.Configuration(ctx.Configuration)
				   .WriteTo.Console()
				   .WriteTo.Seq("http://localhost:5341")
			   );

var app = builder.Build();

app.UseHttpsRedirection();

// Endpoint ��� ��������� ������
app.MapPost("/status1", async (HttpContext context, ILogger logger) =>
{
	// ������ ����������� �������
	using var reader = new StreamReader(context.Request.Body);
	var modelContent = await reader.ReadToEndAsync();

	// �������� ���������� ���������
	logger.Information("Received message: {ModelContent}", modelContent);

	// � ������, ���� ������ ������ ��� ������������, �������� ������
	if (string.IsNullOrEmpty(modelContent))
	{
		logger.Warning("Received an empty or invalid model.");
		return Results.BadRequest("Received an empty or invalid model.");
	}

	// ���������� �������� ����� � �������� ���
	logger.Information("Successfully processed the model.");
	return Results.Ok("Received and logged the model.");
});

app.Run();
