using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MiniRAG.Api.Services.Embedding
{
	public class EmbeddingService
	{
		private readonly HttpClient _http;
		private readonly string _baseUrl;

		public EmbeddingService(HttpClient http, IConfiguration config)
		{
			_http = http;
			_baseUrl = config["Embeddings:Url"];
			_http.BaseAddress = new Uri(_baseUrl);
		}

		public async Task<float[]> GenerateAsync(string text, string inputType = "query", CancellationToken ct = default)
		{
			var payload = new
			{
				texts = new[] { text },
				input_type = inputType
			};

			using var resp = await _http.PostAsJsonAsync("/embed", payload, ct);
			resp.EnsureSuccessStatusCode();

			var doc = await resp.Content.ReadFromJsonAsync<EmbedResponse>(cancellationToken: ct);
			if (doc?.Embeddings == null || doc.Embeddings.Count == 0)
				throw new InvalidOperationException("Embeddings API returned no vectors.");

			// Ensure float32 array
			return doc.Embeddings[0].Select(x => (float)x).ToArray();
		}

		private sealed class EmbedResponse
		{
			public string? Model { get; set; }
			public int Dim { get; set; }
			public List<List<double>>? Embeddings { get; set; }
		}
	}
}
