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
			var baseUrl = config["LLM:Url"] ?? "http://localhost:11434";
			_http.BaseAddress = new Uri(baseUrl);
			_http.Timeout = TimeSpan.FromMinutes(5); // LLM pode demorar
			_modelName = config["LLM:ModelName"] ?? "llama3.2:3b"; // Modelo leve para começar
		}

		public async Task<string> GenerateResponseAsync(string question, List<Document> relevantDocuments, CancellationToken ct = default)
		{
			// Constrói o contexto com os documentos relevantes
			var context = BuildContext(relevantDocuments);

			// Prompt personalizado para atendimento de vendas
			var prompt = BuildPrompt(question, context);

			var payload = new
			{
				model = _modelName,
				prompt = prompt,
				stream = false,
				options = new
				{
					temperature = 0.7, // Criatividade moderada
					max_tokens = 500,   // Limite de tokens na resposta
					top_p = 0.9
				}
			};

			try
			{
				using var resp = await _http.PostAsJsonAsync("/api/generate", payload, ct);
				resp.EnsureSuccessStatusCode();

				var result = await resp.Content.ReadFromJsonAsync<OllamaResponse>(cancellationToken: ct);
				return result?.Response ?? "Desculpe, não consegui gerar uma resposta no momento.";
			}
			catch (HttpRequestException ex)
			{
				throw new InvalidOperationException($"Erro ao comunicar com o LLM: {ex.Message}", ex);
			}
			catch (TaskCanceledException)
			{
				throw new TimeoutException("Timeout na geração da resposta pelo LLM.");
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

			return $"Modelo {_modelName} baixado com sucesso!";
		}

		private string BuildContext(List<Document> documents)
		{
			if (!documents.Any())
				return "Nenhuma informação específica encontrada na base de conhecimento.";

			var sb = new StringBuilder();
			sb.AppendLine("=== INFORMAÇÕES DA EMPRESA ===");

			for (int i = 0; i < Math.Min(documents.Count, 5); i++) // Máximo 5 documentos
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

		private string BuildPrompt(string question, string context)
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