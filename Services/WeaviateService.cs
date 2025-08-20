using Microsoft.Extensions.Configuration;
using MiniRAG.Api.Models;
using MiniRAG.Api.Weaviate.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

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
		private readonly HttpClient _http;
		private readonly string _className;

		public WeaviateService(HttpClient http, IConfiguration config)
		{
			_http = http;
			var baseUrl = config["Weaviate:Url"];
			_http.BaseAddress = new Uri(baseUrl);
			_className = config["Weaviate:ClassName"];
		}

		public async Task<bool> ClassExistsAsync(CancellationToken ct = default)
		{
			try
			{
				using var resp = await _http.GetAsync("/v1/schema", ct);
				resp.EnsureSuccessStatusCode();
				var schema = await resp.Content.ReadFromJsonAsync<WeaviateSchemaResponse>(ct);

				return schema?.Classes?.Any(c => c.Class == _className) ?? false;
			}
			catch
			{
				return false;
			}
		}

		public async Task CreateClassAsync(CancellationToken ct = default)
		{
			var classSchema = new WeaviateClassSchema
			{
				Class = _className,
				Description = "Document chunks for RAG system",
				Vectorizer = "none",
				Properties = new[]
				{
					new WeaviateProperty
					{
						Name = "text",
						DataType = new[] { "text" },
						Description = "The document text content"
					},
					new WeaviateProperty
					{
						Name = "source",
						DataType = new[] { "text" },
						Description = "Source identifier for the document"
					}
				}
			};

			using var resp = await _http.PostAsJsonAsync("/v1/schema", classSchema, ct);
			resp.EnsureSuccessStatusCode();
		}

		public async Task<string> AddDocumentAsync(string text, string source, float[] embedding, CancellationToken ct = default)
		{
			var document = new
			{
				@class = _className,
				properties = new { text, source },
				vector = embedding
			};

			using var resp = await _http.PostAsJsonAsync("/v1/objects", document, ct);
			resp.EnsureSuccessStatusCode();
			var result = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
			return result.TryGetProperty("id", out var id) ? id.GetString() ?? string.Empty : string.Empty;
		}

		public async Task<List<Document>> SearchDocsBySimilarity(float[] embedding, int topK = 10, CancellationToken ct = default)
		{
			// Usar o endpoint de objects com nearVector via query parameters
			var queryParams = new List<string>
			{
				$"class={_className}",
				$"limit={topK}",
				"fields=text,source,_additional{id,distance,certainty}"
			};

			// Para nearVector, usar como body em POST
			var nearVectorPayload = new
			{
				nearVector = new
				{
					vector = embedding
				}
			};

			var query = string.Join("&", queryParams);
			using var resp = await _http.PostAsJsonAsync($"/v1/objects?{query}", nearVectorPayload, ct);
			resp.EnsureSuccessStatusCode();

			var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
			var list = new List<Document>();

			if (!json.TryGetProperty("data", out var data) ||
				!data.TryGetProperty("Get", out var getObj) ||
				!getObj.TryGetProperty(_className, out var results))
			{
				return list;
			}

			foreach (var item in results.EnumerateArray())
			{
				var doc = new Document
				{
					Id = GetPropertyValue(item, "_additional", "id")?.GetString() ?? string.Empty,
					Texto = GetPropertyValue(item, "text")?.GetString(),
					Source = GetPropertyValue(item, "source")?.GetString(),
					Embedding = embedding
				};
				list.Add(doc);
			}

			return list;
		}

		public async Task<int> GetDocumentCountAsync(CancellationToken ct = default)
		{
			try
			{
				// Buscar com limite alto apenas para contar
				using var resp = await _http.GetAsync($"/v1/objects?class={_className}&limit=10000", ct);
				resp.EnsureSuccessStatusCode();

				var result = await resp.Content.ReadFromJsonAsync<WeaviateObjectsResponse>(ct);
				return result?.TotalResults ?? result?.Objects?.Length ?? 0;
			}
			catch
			{
				return 0;
			}
		}

		public async Task<int> ClearAllDocumentsAsync(CancellationToken ct = default)
		{
			try
			{
				var countBefore = await GetDocumentCountAsync(ct);
				if (countBefore == 0) return 0;

				// Usar batch delete para limpar todos os objetos da classe
				var batchDeleteRequest = new WeaviateBatchDeleteRequest
				{
					Match = new WeaviateDeleteMatch
					{
						Class = _className
					},
					Output = "minimal"
				};

				using var resp = await _http.PostAsJsonAsync("/v1/batch/objects", batchDeleteRequest, ct);

				if (resp.IsSuccessStatusCode)
				{
					// Aguardar processamento
					await Task.Delay(1000, ct);
					var countAfter = await GetDocumentCountAsync(ct);
					return countBefore - countAfter;
				}

				// Fallback: recriar classe
				await RecreateClassAsync(ct);
				return countBefore;
			}
			catch
			{
				// Em caso de erro, tentar recriar classe
				await RecreateClassAsync(ct);
				return 0;
			}
		}

		public async Task<int> ClearDocumentsBySourcePrefixAsync(string sourcePrefix, CancellationToken ct = default)
		{
			try
			{
				var countBefore = await CountDocumentsBySourcePrefixAsync(sourcePrefix, ct);
				if (countBefore == 0) return 0;

				// Batch delete com filtro por source
				var batchDeleteRequest = new WeaviateBatchDeleteRequest
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

				using var resp = await _http.PostAsJsonAsync("/v1/batch/objects", batchDeleteRequest, ct);

				if (resp.IsSuccessStatusCode)
				{
					// Aguardar processamento
					await Task.Delay(1000, ct);
					var countAfter = await CountDocumentsBySourcePrefixAsync(sourcePrefix, ct);
					return countBefore - countAfter;
				}

				return 0;
			}
			catch
			{
				return 0;
			}
		}

		public async Task<int> CountDocumentsBySourcePrefixAsync(string sourcePrefix, CancellationToken ct = default)
		{
			try
			{
				var encodedFilter = HttpUtility.UrlEncode($"{{\"path\":[\"source\"],\"operator\":\"Like\",\"valueText\":\"{sourcePrefix}*\"}}");

				using var resp = await _http.GetAsync($"/v1/objects?class={_className}&where={encodedFilter}&limit=10000", ct);
				resp.EnsureSuccessStatusCode();

				var result = await resp.Content.ReadFromJsonAsync<WeaviateObjectsResponse>(ct);
				return result?.Objects?.Length ?? 0;
			}
			catch
			{
				return 0;
			}
		}

		public async Task<string> RecreateClassAsync(CancellationToken ct = default)
		{
			try
			{
				var messages = new List<string>();
				var existsBefore = await ClassExistsAsync(ct);
				var countBefore = 0;

				if (existsBefore)
				{
					countBefore = await GetDocumentCountAsync(ct);
					messages.Add($"Classe existia com {countBefore} documentos");

					// Deletar classe existente
					using var deleteResp = await _http.DeleteAsync($"/v1/schema/{_className}", ct);
					if (deleteResp.IsSuccessStatusCode || deleteResp.StatusCode == System.Net.HttpStatusCode.NotFound)
					{
						messages.Add("Classe anterior removida");
					}
				}

				// Aguardar deleção
				await Task.Delay(500, ct);

				// Recriar classe
				await CreateClassAsync(ct);
				messages.Add("Nova classe criada");

				// Verificar criação
				await Task.Delay(500, ct);
				var existsAfter = await ClassExistsAsync(ct);

				if (existsAfter)
				{
					messages.Add("Verificação: classe recriada com sucesso");
				}
				else
				{
					messages.Add("ATENÇÃO: classe pode não ter sido criada corretamente");
				}

				return string.Join(", ", messages);
			}
			catch (Exception ex)
			{
				return $"Erro ao recriar classe: {ex.Message}";
			}
		}

		private static JsonElement? GetPropertyValue(JsonElement element, params string[] path)
		{
			var current = element;
			foreach (var prop in path)
			{
				if (!current.TryGetProperty(prop, out current))
					return null;
			}
			return current;
		}
	} 
}

///TODO: Adicionar ao controle de versão. | Criar dados mais adequados a realidade e testar