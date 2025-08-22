using Microsoft.AspNetCore.Mvc;
using MiniRAG.Api.RAG.Models;
using MiniRAG.Api.Services.Embedding;
using MiniRAG.Api.Services.LLama;
using MiniRAG.Api.Services.Weaviate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MiniRAG.Api.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class RAGController : ControllerBase
	{
		private readonly EmbeddingService _embeddingService;
		private readonly IWeaviateService _weaviateService;
		private readonly LLMService _llmService;

		public RAGController(EmbeddingService embeddingService, IWeaviateService weaviateService, LLMService llmService)
		{
			_embeddingService = embeddingService;
			_weaviateService = weaviateService;
			_llmService = llmService;
		}

		/// <summary>
		/// Full Pipeline RAG - Question + Generated answer under the given context
		/// </summary>
		[HttpPost("ask")]
		public async Task<IActionResult> Ask([FromBody] RAGRequest request)
		{
			try
			{
				// 1. Gerar embedding da pergunta
				var embedding = await _embeddingService.GenerateAsync(request.Question, "query");

				// 2. Buscar documentos similares
				var documents = await _weaviateService.SearchDocsBySimilarity(embedding, request.TopK);

				// 3. Gerar resposta usando LLM com contexto
				var response = await _llmService.GenerateResponseAsync(request.Question, documents);

				return Ok(new RAGResponse
				{
					Question = request.Question,
					Answer = response,
					RelevantDocuments = documents,
					DocumentCount = documents.Count
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "RAG pipeline error" });
			}
		}

		/// <summary>
		/// Search for similar documents (no answer generation)
		/// </summary>
		[HttpPost("search")]
		public async Task<IActionResult> Search([FromBody] SearchRequest request)
		{
			try
			{
				var embedding = await _embeddingService.GenerateAsync(request.Query, "query");
				var documents = await _weaviateService.SearchDocsBySimilarity(embedding, request.TopK);
				return Ok(documents);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message });
			}
		}

		/// <summary>
		/// Verifica status de todos os serviços
		/// </summary>
		[HttpGet("health")]
		public async Task<IActionResult> Health()
		{
			try
			{
				var weaviateClassExists = await _weaviateService.ClassExistsAsync();
				var llmAvailable = await _llmService.IsModelAvailableAsync();

				// Contar documentos se a classe existir
				var documentCount = weaviateClassExists ?
					await _weaviateService.GetDocumentCountAsync() : 0;

				var health = new
				{
					API = "OK",
					Timestamp = DateTime.UtcNow,
					Services = new
					{
						Embeddings = "OK", // Assumindo que está funcionando se chegou até aqui
						Weaviate = weaviateClassExists ? "OK - Class exists" : "Warning - Class not found",
						LLM = llmAvailable ? "OK" : "Warning - Model not available"
					},
					Data = new
					{
						DocumentCount = documentCount,
						HasData = documentCount > 0
					}
				};

				return Ok(health);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Services status check failed" });
			}
		}

		#region Data Management

		/// <summary>
		/// Add some docs for testing and debugging porposes
		/// </summary>
		[HttpPost("seed")]
		public async Task<IActionResult> SeedTestData()
		{
			try
			{
				if (!await _weaviateService.ClassExistsAsync())
				{
					return BadRequest(new { error = " DocumentChunk class not found. Run \"Initialize\" method first" });
				}

				var testDocuments = new[]
										{
											"Bloquinho personalizado tipo livro (10x7cm, 50 folhas) com mini-lápis, celofane, fita de cetim e tag de agradecimento custa R$6,49 por unidade.",
											"Bloquinho personalizado com wire-o branco (10x7cm, 35 folhas) e mini-lápis, celofane, fita e tag de agradecimento custa R$9,35 por unidade.",
											"Mini caneca personalizada de 15ml custa R$7,65 cada.",
											"Caneca de café personalizada de 50ml custa R$13,20 cada.",
											"Imagem de Nossa Senhora (10cm) personalizada no manto e frase na base custa R$34,90 por unidade.",
											"Aparador de joias oval (10x7cm) personalizado na frente custa R$23,20; frente e verso custa R$27,50.",
											"Aparador de joias redondo ondulado 8cm custa R$15,75 somente frente, ou R$19,90 frente e verso.",
											"Porta joias 6cm personalizado somente na tampa custa R$17,98; versões com filete ouro/prata ou personalização extra têm acréscimos.",
											"Porta joias 8cm personalizado somente na tampa custa R$28,76; opções adicionais incluem filete e personalização lateral ou fundo.",
											"Garrafinha de porcelana personalizada custa R$16,90 frente ou R$21,20 frente e verso.",
											"Vaso Manilha (11,5cm) personalizado completo custa R$38,98; detalhes em ouro ou prata custam +R$18,00.",
											"Vaso Funil (10cm) personalizado completo custa R$40,98; detalhes em ouro ou prata custam +R$18,00.",
											"Vela em potinho de porcelana personalizada custa R$19,80 (frente), R$24,10 (frente e verso) ou apenas potinho por R$12,98.",
											"Toalhinha personalizada custa R$7,98; com fita de cetim e cartão custa R$10,98; com cofrinho custa R$13,98.",
											"Quebra-cabeça personalizado (21x15cm, 12 peças) com celofane, fita e tag custa R$8,90; com latinha personalizada custa R$14,90.",
											"Cofrinho personalizado (tampa branca) custa R$9,98.",
											"Tubolata personalizada com laço custa R$13,00.",
											"Kit embalagem organza + fita + tag custa R$3,90; celofane + fita + tag custa R$2,90."
										};


				var addedCount = 0;
				var errors = new List<string>();

				foreach (var (text, index) in testDocuments.Select((t, i) => (t, i)))
				{
					try
					{
						var embedding = await _embeddingService.GenerateAsync(text, "passage");
						var docId = await _weaviateService.AddDocumentAsync(text, $"test_seed_doc_{index}", embedding);

						if (!string.IsNullOrEmpty(docId))
						{
							addedCount++;
						}
						else
						{
							errors.Add($"Document {index} retuned an invalid ID");
						}
					}
					catch (Exception ex)
					{
						errors.Add($"Document error: {index}: {ex.Message}");
					}
				}

				var result = new
				{
					message = $"Seeding process done.",
					success = new
					{
						added = addedCount,
						total = testDocuments.Length
					},
					errors = errors.Any() ? errors : null
				};

				return Ok(result);
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Failed adding testing documents" });
			}
		}

		/// <summary>
		/// Remove todos os documentos da base de dados
		/// </summary>
		[HttpDelete("clear")]
		public async Task<IActionResult> ClearAllData()
		{
			try
			{
				if (!await _weaviateService.ClassExistsAsync())
				{
					return BadRequest(new { error = "DocumentChunk class doesn't exist. Nothing to clear." });
				}

				var countBefore = await _weaviateService.GetDocumentCountAsync();

				if (countBefore == 0)
				{
					return Ok(new { message = "Database already empty.", documentsRemoved = 0 });
				}

				var removedCount = await _weaviateService.ClearAllDocumentsAsync();
				var countAfter = await _weaviateService.GetDocumentCountAsync();

				return Ok(new
				{
					message = "Dadabase cleared",
					documentsRemoved = removedCount,
					before = countBefore,
					after = countAfter,
					timestamp = DateTime.UtcNow
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Fail clearing database" });
			}
		}

		/// <summary>
		/// Remove apenas documentos de teste (com source começando com "test_")
		/// </summary>
		[HttpDelete("clear/test-data")]
		public async Task<IActionResult> ClearTestData()
		{
			try
			{
				if (!await _weaviateService.ClassExistsAsync())
				{
					return BadRequest(new { error = "DocumentChunk class doesn't exist" });
				}

				var removedCount = await _weaviateService.ClearDocumentsBySourcePrefixAsync("test_");

				return Ok(new
				{
					message = removedCount > 0 ? "Demo data removed successfully" : "No test data found",
					documentsRemoved = removedCount,
					timestamp = DateTime.UtcNow
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Failed to clear test data" });
			}
		}

		/// <summary>
		/// Recria a classe do Weaviate (remove tudo e recria estrutura)
		/// </summary>
		[HttpPost("recreate-schema")]
		public async Task<IActionResult> RecreateSchema()
		{
			try
			{
				var result = await _weaviateService.RecreateClassAsync();

				return Ok(new
				{
					message = "Schema recreated successfully",
					details = result,
					timestamp = DateTime.UtcNow
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Schema recriation failed" });
			}
		}

		/// <summary>
		/// Retorna estatísticas da base de dados
		/// </summary>
		[HttpGet("stats")]
		public async Task<IActionResult> GetStats()
		{
			try
			{
				if (!await _weaviateService.ClassExistsAsync())
				{
					return Ok(new { message = "Class doesn't exist", documentCount = 0 });
				}

				var totalDocuments = await _weaviateService.GetDocumentCountAsync();
				var testDocuments = await _weaviateService.CountDocumentsBySourcePrefixAsync("test_");

				return Ok(new
				{
					totalDocuments,
					testDocuments,
					productionDocuments = totalDocuments - testDocuments,
					hasData = totalDocuments > 0,
					timestamp = DateTime.UtcNow
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Fail to obtain status" });
			}
		}

		#endregion

		#region Setup and Maintenance

		/// <summary>
		/// Força download do modelo LLM (primeira vez)
		/// </summary>
		[HttpPost("setup")]
		public async Task<IActionResult> Setup()
		{
			try
			{
				if (await _llmService.IsModelAvailableAsync())
				{
					return Ok(new { message = "LLM model already available!" });
				}

				var result = await _llmService.PullModelAsync();
				return Ok(new { message = result });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Failed to download LLM model" });
			}
		}

		/// <summary>
		/// Inicialização completa do sistema (schema + modelo)
		/// </summary>
		[HttpPost("initialize")]
		public async Task<IActionResult> Initialize()
		{
			try
			{
				var results = new List<string>();
				var errors = new List<string>();

				// 1. Verificar/Criar classe no Weaviate
				try
				{
					if (!await _weaviateService.ClassExistsAsync())
					{
						await _weaviateService.CreateClassAsync();
						results.Add("DocumentChunk class created on Weaviate");
					}
					else
					{
						results.Add("DocumentChunk class already exists on Weaviate");
					}
				}
				catch (Exception ex)
				{
					errors.Add($"Fail to create class on Weaviate: {ex.Message}");
				}

				// 2. Verificar modelo LLM
				try
				{
					if (!await _llmService.IsModelAvailableAsync())
					{
						var pullResult = await _llmService.PullModelAsync();
						results.Add($"LLM model downloaded: {pullResult}");
					}
					else
					{
						results.Add("LLM model already available");
					}
				}
				catch (Exception ex)
				{
					errors.Add($"Fail to check/download LLM model: {ex.Message}");
				}

				var response = new
				{
					message = errors.Count > 0 ? "Partial initialization" : "System successfully initiated",
					success = results,
					errors = errors.Count > 0 ? errors : null,
					timestamp = DateTime.UtcNow
				};

				return errors.Count > 0 ? StatusCode(207, response) : Ok(response); // 207 = Multi-Status
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Failed starting service" });
			}
		}

		#endregion

		/// <summary>
		/// Endpoint simples para teste
		/// </summary>
		[HttpGet("ping")]
		public IActionResult Ping()
		{
			return Ok(new { message = "MiniRAG API is running!", timestamp = DateTime.UtcNow });
		}
	}
}
 