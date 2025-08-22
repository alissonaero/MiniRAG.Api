using Microsoft.Extensions.Configuration;
using MiniRAG.Api.Core.Helpers;
using MiniRAG.Api.Models;
using MiniRAG.Api.Weaviate.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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
			return result?.Id ?? string.Empty;
		}

		public async Task<List<Document>> SearchDocsBySimilarity(float[] embedding, int topK = 10, CancellationToken ct = default)
		{
			var results = await _client.SearchByVectorAsync(embedding, topK, ct);
			var documents = new List<Document>();

			foreach (var item in results)
			{
				documents.Add(new Document
				{
					Id = item.Additional?.Id ?? string.Empty,
					Texto = item.Text,
					Source = item.Source,
					Embedding = embedding
				});
			}

			Console.WriteLine($"Found {documents.Count} documents");
			return documents;
		}

		public async Task<int> GetDocumentCountAsync(CancellationToken ct = default)
		{
			try
			{
				return await _client.CountDocumentsAsync(ct);
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
						Path = ["source"],
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

		public async Task<int> CountDocumentsBySourcePrefixAsync(string sourcePrefix, CancellationToken ct = default)
		{
			try
			{
				return await _client.CountDocumentsBySourcePrefixAsync(sourcePrefix, ct);
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

			if (existsBefore)
			{
				int countBefore = await GetDocumentCountAsync(ct);
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