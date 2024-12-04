using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using Serilog;

namespace queue
{
	public class QueueService
	{
		private readonly IConnectionFactory _connectionFactory;
		private readonly ILogger _logger;
		private readonly Dictionary<string, string> _queues = new()
		{
			{ "server1", "queue1" },
			{ "server2", "queue2" },
			{ "server3", "queue3" },
			{ "sftp", "queue3" }
		};

		public QueueService(
			IConnectionFactory connectionFactory,
			ILogger logger)
		{
			_connectionFactory = connectionFactory;
			_logger = logger;
		}

		public bool QueueExists(string server) => _queues.ContainsKey(server);

		public void PublishMessage(string server, string messageContent)
		{
			if (!_queues.TryGetValue(server, out var queueName))
			{
				_logger.Warning("Попытка отправить сообщение на несуществующую очередь: {Server}", server);
				return;
			}

			// Проверка на XML
			if (IsXml(messageContent))
			{
				messageContent = ConvertXmlToJson(messageContent);
				_logger.Information("Сообщение было преобразовано из XML в JSON.");
			}

			var enrichedMessage = new
			{
				Id = Guid.NewGuid(),
				ServerTag = server,
				Content = messageContent
			};

			var message = System.Text.Json.JsonSerializer.Serialize(enrichedMessage);
			var body = Encoding.UTF8.GetBytes(message);

			using var connection = _connectionFactory.CreateConnection();
			using var channel = connection.CreateModel();

			// Убедимся, что очередь существует
			channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

			// Публикуем сообщение в очередь RabbitMQ
			channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);

			_logger.Information("Сообщение с GUID {Id} добавлено в очередь {Server}", enrichedMessage.Id, server);
		}

		private bool IsXml(string content)
		{
			// Проверка на начало XML документа
			return content.TrimStart().StartsWith("<");
		}

		private string ConvertXmlToJson(string xml)
		{
			var xmlDoc = new XmlDocument();

			// Убираем BOM-символы и невидимые символы
			xml = xml.TrimStart(new char[] { '\uFEFF', '\u200B' });

			// Убираем вложенную XML декларацию внутри <model>
			var xmlWithoutInnerDeclaration = Regex.Replace(xml, @"<\?xml.*?\?>", string.Empty);

			// Убираем все до первого тега
			xml = xmlWithoutInnerDeclaration.Substring(xmlWithoutInnerDeclaration.IndexOf('<'));

			// Логируем XML перед загрузкой для диагностики
			_logger.Information("XML перед загрузкой: {XmlContent}", xml);

			try
			{
				xmlDoc.LoadXml(xml);  // Попытка загрузить XML
			}
			catch (XmlException ex)
			{
				_logger.Error("Ошибка при загрузке XML: {ErrorMessage}", ex.Message);
				return JsonConvert.SerializeObject(new { error = "Ошибка при обработке XML", details = ex.Message }, Newtonsoft.Json.Formatting.Indented);
			}

			XmlNode bodyNode = xmlDoc.SelectSingleNode("//*[local-name()='Body']");

			if (bodyNode != null)
			{
				var card112ChangedRequestNode = bodyNode.SelectSingleNode("//*[local-name()='card112ChangedRequest']");

				if (card112ChangedRequestNode != null)
				{
					var jsonSettings = new JsonSerializerSettings
					{
						Formatting = Newtonsoft.Json.Formatting.Indented,
						Converters = { new Newtonsoft.Json.Converters.XmlNodeConverter { OmitRootObject = true } }
					};

					string jsonText = JsonConvert.SerializeObject(card112ChangedRequestNode, jsonSettings);

					var jsonObject = JsonConvert.DeserializeObject<JObject>(jsonText);

					// Убираем все атрибуты, начинающиеся с "@"
					jsonObject.Descendants().OfType<JProperty>()
							  .Where(attr => attr.Name.StartsWith("@"))
							  .ToList()
							  .ForEach(attr => attr.Remove());

					return JsonConvert.SerializeObject(jsonObject, Newtonsoft.Json.Formatting.Indented);
				}
			}

			// Если узел не найден, возвращаем исходное сообщение как JSON
			return JsonConvert.SerializeObject(new { originalMessage = xml }, Newtonsoft.Json.Formatting.Indented);
		}
	}
}
