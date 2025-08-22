using Microsoft.Extensions.Configuration;
using MiniRAG.Api.Weaviate.Models;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;


namespace MiniRAG.Api.Clients.Weaviate;
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

	//Generic method to execute any GraphQL query
	private async Task<T> ExecuteGraphQLAsync<T>(string query, CancellationToken ct)
	{
		var payload = new { query };
		var resp = await _http.PostAsJsonAsync(_graphqlEndpoint, payload, ct);
		resp.EnsureSuccessStatusCode();

		var response = await resp.Content.ReadFromJsonAsync<WeaviateGraphQLResponse<T>>(ct);

		//Checks for GraphQL errors
		if (response?.Errors?.Length > 0)
		{
			var errorMsg = response.Errors[0].Message;
			throw new InvalidOperationException($"Weaviate GraphQL Error: {errorMsg}");
		}

		return response.Data;
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

	public async Task<WeaviateAddDocumentResponse?> AddDocumentAsync(object document, CancellationToken ct)
	{
		var resp = await _http.PostAsJsonAsync(_objectsEndpoint, document, ct);
		resp.EnsureSuccessStatusCode();
		return await resp.Content.ReadFromJsonAsync<WeaviateAddDocumentResponse>(ct);
	}

	public async Task<WeaviateDocumentResult[]> SearchByVectorAsync(float[] embedding, int topK, CancellationToken ct)
	{
		var query = WeaviateQueryBuilder.Create()
			.SearchDocuments(_className)
			.NearVector(embedding)
			.Limit(topK)
			.Build();

		var data = await ExecuteGraphQLAsync<WeaviateGetData>(query, ct);

		return data?.Get?.TryGetValue(_className, out var results) == true ? results : [];
	}

	public async Task<WeaviateDocumentResult[]> GetAllDocumentsAsync(CancellationToken ct)
	{
		var query = WeaviateQueryBuilder.Create()
			.GetAllDocuments(_className)
			.Build();

		var data = await ExecuteGraphQLAsync<WeaviateGetData>(query, ct);

		return data?.Get?.TryGetValue(_className, out var results) == true ? results : [];
	}

	public async Task<int> CountDocumentsAsync(CancellationToken ct)
	{
		var query = WeaviateQueryBuilder.Create()
			.CountDocuments(_className)
			.Build();

		var data = await ExecuteGraphQLAsync<WeaviateAggregateData>(query, ct);

		if (data?.Aggregate?.TryGetValue(_className, out var results) == true &&
			results.Length > 0 &&
			results[0].Meta != null)
		{
			return results[0].Meta.Count;
		}

		return 0;
	}

	public async Task<int> CountDocumentsBySourcePrefixAsync(string sourcePrefix, CancellationToken ct)
	{
		var query = WeaviateQueryBuilder.Create()
			.CountDocuments(_className)
			.WhereLike("source", $"{sourcePrefix}*")
			.Build();

		var data = await ExecuteGraphQLAsync<WeaviateAggregateData>(query, ct);

		if (data?.Aggregate?.TryGetValue(_className, out var results) == true &&
			results.Length > 0 &&
			results[0].Meta != null)
		{
			return results[0].Meta.Count;
		}

		return 0;
	}

	public async Task<WeaviateDocumentResult[]> SearchWithCustomQuery(WeaviateQueryBuilder queryBuilder, CancellationToken ct)
	{
		var query = queryBuilder.Build();
		var data = await ExecuteGraphQLAsync<WeaviateGetData>(query, ct);

		return data?.Get?.TryGetValue(_className, out var results) == true ? results : [];
	}

	//Let's keep the REST methods for those operation where there's no equivalent GraphQL
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