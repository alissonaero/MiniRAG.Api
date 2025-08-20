using Microsoft.Extensions.Configuration;
using MiniRAG.Api.Weaviate.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class WeaviateClient
{
	private readonly HttpClient _http;
	private readonly string _className;
	private readonly string _schemaEndpoint;
	private readonly string _objectsEndpoint;
	private readonly string _batchEndpoint;
	private readonly string _graphqlEndpoint;
	private readonly string _defaultLimit;

	public WeaviateClient(HttpClient http, IConfiguration config)
	{
		_http = http;
		_http.BaseAddress = new Uri(config["Weaviate:Url"]);
		_className = config["Weaviate:ClassName"];
		_schemaEndpoint = config["Weaviate:Endpoints:Schema"];
		_objectsEndpoint = config["Weaviate:Endpoints:Objects"];
		_batchEndpoint = config["Weaviate:Endpoints:Batch"];
		_graphqlEndpoint = config["Weaviate:Endpoints:GraphQL"] ?? "/v1/graphql";
		_defaultLimit = config["Weaviate:DefaultLimit"] ?? "1000";
	}

	public async Task<WeaviateSchemaResponse?> GetSchemaAsync(CancellationToken ct)
	{
		var resp = await _http.GetAsync(_schemaEndpoint, ct);
		resp.EnsureSuccessStatusCode();
		return await resp.Content.ReadFromJsonAsync<WeaviateSchemaResponse>(ct);
	}

	public async Task CreateClassAsync(WeaviateClassSchema schema, CancellationToken ct)
	{
		var resp = await _http.PostAsJsonAsync(_schemaEndpoint, schema, ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task DeleteClassAsync(CancellationToken ct)
	{
		var resp = await _http.DeleteAsync($"{_schemaEndpoint}/{_className}", ct);
		resp.EnsureSuccessStatusCode();
	}

	public async Task<JsonElement?> AddDocumentAsync(object document, CancellationToken ct)
	{
		var resp = await _http.PostAsJsonAsync(_objectsEndpoint, document, ct);
		resp.EnsureSuccessStatusCode();
		return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
	}
 
	public async Task<JsonElement?> SearchByVectorAsync(float[] embedding, int topK, CancellationToken ct)
	{
		var query = $@"
		{{
			Get {{
				{_className}(
					nearVector: {{
						vector: [{string.Join(",", embedding.Select(f => f.ToString("G", System.Globalization.CultureInfo.InvariantCulture)))}]
					}}
					limit: {topK}
				) {{
					text
					source
					_additional {{
						id
						distance
						certainty
					}}
				}}
			}}
		}}";

		var payload = new { query = query.Replace("\n", "").Replace("\t", "") };

		var resp = await _http.PostAsJsonAsync(_graphqlEndpoint, payload, ct);
		resp.EnsureSuccessStatusCode();

		return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
	}

	public async Task<JsonElement?> GetAllDocumentsAsync(CancellationToken ct)
	{
		var query = $@"
		{{
			Get {{
				{_className}(limit: 10000) {{
					text
					source
					_additional {{
						id
					}}
				}}
			}}
		}}";

		var payload = new { query = query.Replace("\n", "").Replace("\t", "") };

		var resp = await _http.PostAsJsonAsync(_graphqlEndpoint, payload, ct);
		resp.EnsureSuccessStatusCode();

		return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
	}

	// NOVO: Contar documentos usando Aggregate
	public async Task<JsonElement?> CountDocumentsAsync(CancellationToken ct)
	{
		var query = $@"
		{{
			Aggregate {{
				{_className} {{
					meta {{
						count
					}}
				}}
			}}
		}}";

		var payload = new { query = query.Replace("\n", "").Replace("\t", "") };

		var resp = await _http.PostAsJsonAsync(_graphqlEndpoint, payload, ct);
		resp.EnsureSuccessStatusCode();

		return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
	}

	///TODO: Implement 
	public async Task<JsonElement?> CountDocumentsBySourcePrefixAsync(string sourcePrefix, CancellationToken ct)
	{
		var query = $@"
		{{
			Aggregate {{
				{_className}(
					where: {{
						path: [""source""]
						operator: Like
						valueText: ""{sourcePrefix}*""
					}}
				) {{
					meta {{
						count
					}}
				}}
			}}
		}}";

		var payload = new { query = query.Replace("\n", "").Replace("\t", "") };

		var resp = await _http.PostAsJsonAsync(_graphqlEndpoint, payload, ct);
		resp.EnsureSuccessStatusCode();

		return await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
	}

	// Mantém os métodos REST para operações que não têm equivalente GraphQL
	public async Task<WeaviateObjectsResponse?> GetObjectsAsync(string filter, CancellationToken ct)
	{
		var url = $"{_objectsEndpoint}?class={_className}&where={filter}&limit={_defaultLimit}";
		var resp = await _http.GetAsync(url, ct);
		resp.EnsureSuccessStatusCode();
		return await resp.Content.ReadFromJsonAsync<WeaviateObjectsResponse>(ct);
	}

	public async Task<WeaviateObjectsResponse?> GetAllObjectsAsync(CancellationToken ct)
	{
		var url = $"{_objectsEndpoint}?class={_className}&limit=10000";
		var resp = await _http.GetAsync(url, ct);
		resp.EnsureSuccessStatusCode();
		return await resp.Content.ReadFromJsonAsync<WeaviateObjectsResponse>(ct);
	}

	public async Task<HttpResponseMessage> BatchDeleteAsync(WeaviateBatchDeleteRequest request, CancellationToken ct)
	{
		return await _http.PostAsJsonAsync(_batchEndpoint, request, ct);
	}
}