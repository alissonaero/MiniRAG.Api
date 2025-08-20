using Microsoft.Extensions.Configuration;
using MiniRAG.Api.Core.Helpers;
using MiniRAG.Api.Models;
using MiniRAG.Api.Weaviate.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MiniRAG.Api.Services.Weaviate
{
	public interface IWeaviateService
	{
		Task<List<Document>> SearchDocsBySimilarity(float[] embedding, int topK = 10, CancellationToken ct = default);
		Task<string> AddDocumentAsync(string text, string source, float[] embedding, CancellationToken ct = default);
		Task<bool> ClassExistsAsync(CancellationToken ct = default);
		Task CreateClassAsync(CancellationToken ct = default);
		Task<int> GetDocumentCountAsync(CancellationToken ct = default);
		Task<int> ClearAllDocumentsAsync(CancellationToken ct = default);
		Task<int> ClearDocumentsBySourcePrefixAsync(string sourcePrefix, CancellationToken ct = default);
		Task<int> CountDocumentsBySourcePrefixAsync(string sourcePrefix, CancellationToken ct = default);
		Task<string> RecreateClassAsync(CancellationToken ct = default);
	}

	public class WeaviateService : IWeaviateService
	{
		private readonly WeaviateClient _client;
		private readonly string _className;

		public WeaviateService(WeaviateClient client, IConfiguration config)
		{
			_client = client;
			_className = config["Weaviate:ClassName"];
		}

		public async Task<bool> ClassExistsAsync(CancellationToken ct = default)
		{
			var schema = await _client.GetSchemaAsync(ct);
			return schema?.Classes?.Any(c => c.Class == _className) ?? false;
		}

		public async Task CreateClassAsync(CancellationToken ct = default)
		{
			var schema = new WeaviateClassSchema
			{
				Class = _className,
				Description = "Document chunks for RAG system",
				Vectorizer = "none",
				Properties =
				[
					new WeaviateProperty { Name = "text", DataType = ["text"], Description = "The document text content" },
					new WeaviateProperty { Name = "source", DataType = ["text"], Description = "Source identifier for the document" }
				]
			};

			await _client.CreateClassAsync(schema, ct);
		}

		public async Task<string> AddDocumentAsync(string text, string source, float[] embedding, CancellationToken ct = default)
		{
			var document = new
			{
				@class = _className,
				properties = new { text, source },
				vector = embedding
			};

			var result = await _client.AddDocumentAsync(document, ct);
			return result?.TryGetProperty("id", out var id) == true ? id.GetString() ?? string.Empty : string.Empty;
		}

		public async Task<List<Document>> SearchDocsBySimilarity(float[] embedding, int topK = 10, CancellationToken ct = default)
		{
			var json = await _client.SearchByVectorAsync(embedding, topK, ct);
			var documents = new List<Document>();

			if (json is not JsonElement root)
				return documents;

			// Verificar se há erros GraphQL
			if (root.TryGetProperty("errors", out var errors))
			{
				var errorMsg = errors.EnumerateArray().FirstOrDefault().GetProperty("message").GetString();
				throw new InvalidOperationException($"Weaviate GraphQL Error: {errorMsg}");
			}

			// Parse da resposta GraphQL
			if (!root.TryGetProperty("data", out var data) ||
				!data.TryGetProperty("Get", out var getObj) ||
				!getObj.TryGetProperty(_className, out var results) ||
				results.ValueKind != JsonValueKind.Array)
			{
				return documents;
			}

			foreach (var item in results.EnumerateArray())
			{
				var doc = new Document
				{
					Id = item.TryGetProperty("_additional", out var additional) && additional.TryGetProperty("id", out var idProp)
						? idProp.GetString() ?? string.Empty
						: string.Empty,

					Texto = item.TryGetProperty("text", out var textProp) ? textProp.GetString() : null,
					Source = item.TryGetProperty("source", out var sourceProp) ? sourceProp.GetString() : null,
					Embedding = embedding
				};

				documents.Add(doc);
			}

			Console.WriteLine($"Found {documents.Count} documents");
			return documents;
		}

		// ATUALIZADO: Usar GraphQL Aggregate para contar
		public async Task<int> GetDocumentCountAsync(CancellationToken ct = default)
		{
			try
			{
				var json = await _client.CountDocumentsAsync(ct);

				if (json is not JsonElement root)
					return 0;

				// Parse da resposta do Aggregate
				if (root.TryGetProperty("data", out var data) &&
					data.TryGetProperty("Aggregate", out var aggregateObj) &&
					aggregateObj.TryGetProperty(_className, out var classArray) &&
					classArray.ValueKind == JsonValueKind.Array)
				{
					var firstItem = classArray.EnumerateArray().FirstOrDefault();
					if (firstItem.TryGetProperty("meta", out var meta) &&
						meta.TryGetProperty("count", out var countProp))
					{
						return countProp.GetInt32();
					}
				}

				// Fallback para o método REST se GraphQL falhar
				var result = await _client.GetAllObjectsAsync(ct);
				return result?.TotalResults ?? result?.Objects?.Length ?? 0;
			}
			catch
			{
				// Fallback para o método REST em caso de erro
				var result = await _client.GetAllObjectsAsync(ct);
				return result?.TotalResults ?? result?.Objects?.Length ?? 0;
			}
		}

		public async Task<int> ClearAllDocumentsAsync(CancellationToken ct = default)
		{
			var countBefore = await GetDocumentCountAsync(ct);
			if (countBefore == 0) return 0;

			var request = new WeaviateBatchDeleteRequest
			{
				Match = new WeaviateDeleteMatch { Class = _className },
				Output = "minimal"
			};

			var resp = await _client.BatchDeleteAsync(request, ct);
			if (resp.IsSuccessStatusCode)
			{
				await Task.Delay(1000, ct);
				var countAfter = await GetDocumentCountAsync(ct);
				return countBefore - countAfter;
			}

			await RecreateClassAsync(ct);
			return countBefore;
		}

		public async Task<int> ClearDocumentsBySourcePrefixAsync(string sourcePrefix, CancellationToken ct = default)
		{
			var countBefore = await CountDocumentsBySourcePrefixAsync(sourcePrefix, ct);
			if (countBefore == 0) return 0;

			var request = new WeaviateBatchDeleteRequest
			{
				Match = new WeaviateDeleteMatch
				{
					Class = _className,
					Where = new WeaviateWhereFilter
					{
						Path = new[] { "source" },
						Operator = "Like",
						ValueText = $"{sourcePrefix}*"
					}
				},
				Output = "minimal"
			};

			var resp = await _client.BatchDeleteAsync(request, ct);
			if (resp.IsSuccessStatusCode)
			{
				await Task.Delay(1000, ct);
				var countAfter = await CountDocumentsBySourcePrefixAsync(sourcePrefix, ct);
				return countBefore - countAfter;
			}

			return 0;
		}

		// ATUALIZADO: Usar GraphQL Aggregate para contar por filtro
		public async Task<int> CountDocumentsBySourcePrefixAsync(string sourcePrefix, CancellationToken ct = default)
		{
			try
			{
				var json = await _client.CountDocumentsBySourcePrefixAsync(sourcePrefix, ct);

				if (json is not JsonElement root)
					return 0;

				// Parse da resposta do Aggregate
				if (root.TryGetProperty("data", out var data) &&
					data.TryGetProperty("Aggregate", out var aggregateObj) &&
					aggregateObj.TryGetProperty(_className, out var classArray) &&
					classArray.ValueKind == JsonValueKind.Array)
				{
					var firstItem = classArray.EnumerateArray().FirstOrDefault();
					if (firstItem.TryGetProperty("meta", out var meta) &&
						meta.TryGetProperty("count", out var countProp))
					{
						return countProp.GetInt32();
					}
				}

				// Fallback para o método REST se GraphQL falhar
				var encodedFilter = WeaviateHelpers.BuildWeaviateFilter("source", "Like", $"{sourcePrefix}*");
				var result = await _client.GetObjectsAsync(encodedFilter, ct);
				return result?.Objects?.Length ?? 0;
			}
			catch
			{
				// Fallback para o método REST em caso de erro
				var encodedFilter = WeaviateHelpers.BuildWeaviateFilter("source", "Like", $"{sourcePrefix}*");
				var result = await _client.GetObjectsAsync(encodedFilter, ct);
				return result?.Objects?.Length ?? 0;
			}
		}

		public async Task<string> RecreateClassAsync(CancellationToken ct = default)
		{
			var messages = new List<string>();
			var existsBefore = await ClassExistsAsync(ct);
			var countBefore = 0;

			if (existsBefore)
			{
				countBefore = await GetDocumentCountAsync(ct);
				messages.Add($"Previous existing class with {countBefore} documents");
				await _client.DeleteClassAsync(ct);
				messages.Add("Previous class removed");
			}

			await Task.Delay(500, ct);
			await CreateClassAsync(ct);
			messages.Add("New class created");

			await Task.Delay(500, ct);
			var existsAfter = await ClassExistsAsync(ct);
			messages.Add(existsAfter ? "Check: class successfully recreated" : "WARNING: class may not have been created correctly");

			return string.Join(", ", messages);
		}
	}
}

///TODO: Adicionar ao controle de versão. | Criar dados mais adequados a realidade e testar