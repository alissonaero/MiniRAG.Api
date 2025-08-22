using Microsoft.Extensions.Configuration;
using MiniRAG.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MiniRAG.Api.Services.LLama
{
	public class LLMService
	{
		private readonly HttpClient _http;
		private readonly string _modelName;

		public LLMService(HttpClient http, IConfiguration config)
		{
			_http = http;
			var baseUrl = config["LLM:Url"] ;
			_http.BaseAddress = new Uri(baseUrl);
			_http.Timeout = TimeSpan.FromMinutes(5); // The LLM may take time to respond, especially during the first execution.
			_modelName = config["LLM:ModelName"]; // Let's use a light model just for a inital testing and evaluate how it performs.
		}

		public async Task<string> GenerateResponseAsync(string question, List<Document> relevantDocuments, CancellationToken ct = default)
		{
			var context = BuildContext(relevantDocuments);

			// Contextualized prompt    
			var prompt = BuildPrompt(question, context);

			var payload = new
			{
				model = _modelName,
				prompt,
				stream = false,
				options = new
				{
					temperature = 0.7, // moderate creativity
					max_tokens = 500,   
					top_p = 0.9
				}
			};

			try
			{
				using var resp = await _http.PostAsJsonAsync("/api/generate", payload, ct);
				resp.EnsureSuccessStatusCode();

				var result = await resp.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: ct);
				return result?.Response ?? "Desculpe, não consegui gerar uma resposta no momento."; //Portuguese answers since it's for a Brazilian company
			}
			catch (HttpRequestException ex)
			{
				throw new InvalidOperationException($"Fail to to comunicate with the LLM: {ex.Message}", ex);
			}
			catch (TaskCanceledException)
			{
				throw new TimeoutException("LLM answer generation timeout");
			}
		}

		public async Task<bool> IsModelAvailableAsync(CancellationToken ct = default)
		{
			try
			{
				using var resp = await _http.GetAsync("/api/tags", ct);
				if (!resp.IsSuccessStatusCode) return false;

				var result = await resp.Content.ReadFromJsonAsync<TagsResponse>(cancellationToken: ct);
				return result?.Models?.Any(m => m.Name.Contains(_modelName.Split(':')[0])) == true;
			}
			catch
			{
				return false;
			}
		}

		public async Task<string> PullModelAsync(CancellationToken ct = default)
		{
			var payload = new { name = _modelName };

			using var resp = await _http.PostAsJsonAsync("/api/pull", payload, ct);
			resp.EnsureSuccessStatusCode();

			return $"{_modelName} Model downloaded successfully";
		}

		private string BuildContext(List<Document> documents)
		{
			if (documents.Count == 0)
				return "Nenhuma informação específica encontrada na base de conhecimento.";

			var sb = new StringBuilder();
			sb.AppendLine("=== INFORMAÇÕES DA EMPRESA ===");

			for (int i = 0; i < Math.Min(documents.Count, 5); i++) // Max 5 docs
			{
				var doc = documents[i];
				if (!string.IsNullOrWhiteSpace(doc.Texto))
				{
					sb.AppendLine($"Documento {i + 1}:");
					sb.AppendLine(doc.Texto.Trim());
					sb.AppendLine();
				}
			}

			return sb.ToString();
		}

		private static string BuildPrompt(string question, string context)
		{
			return $@"Você é um assistente de vendas especializado em produtos personalizados. Sua função é ajudar clientes com informações sobre preços, prazos e orçamentos.

INSTRUÇÕES:
- Seja cordial, prestativo e profissional
- Use as informações do contexto abaixo para responder
- Se não souber algo específico, seja honesto e sugira que o cliente entre em contato para mais detalhes
- Foque em preços, prazos de entrega e especificações dos produtos
- Mantenha as respostas concisas mas informativas
- Use uma linguagem brasileira natural

{context}

PERGUNTA DO CLIENTE: {question}

RESPOSTA:";
		}

		private sealed class OllamaResponse
		{
			public string? Model { get; set; }
			public string? Response { get; set; }
			public bool Done { get; set; }
			public string? CreatedAt { get; set; }
		}

		private sealed class TagsResponse
		{
			public List<ModelInfo>? Models { get; set; }
		}

		private sealed class ModelInfo
		{
			public string Name { get; set; } = string.Empty;
			public long Size { get; set; }
			public string ModifiedAt { get; set; } = string.Empty;
		}
	}
}