using Microsoft.AspNetCore.Mvc;
using MiniRAG.Api.RAG.Models;
using MiniRAG.Api.Services;
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
		private readonly WeaviateService _weaviateService;
		private readonly LLMService _llmService;

		public RAGController(EmbeddingService embeddingService, WeaviateService weaviateService, LLMService llmService)
		{
			_embeddingService = embeddingService;
			_weaviateService = weaviateService;
			_llmService = llmService;
		}

		/// <summary>
		/// Pipeline RAG completo - Pergunta + Resposta gerada com contexto
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
				return StatusCode(500, new { error = ex.Message, details = "Erro no pipeline RAG" });
			}
		}

		/// <summary>
		/// Busca apenas documentos similares (sem geração de resposta)
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
				return StatusCode(500, new { error = ex.Message, details = "Erro ao verificar status dos serviços" });
			}
		}

		#region Data Management

		/// <summary>
		/// Adiciona documentos de teste para debugging
		/// </summary>
		[HttpPost("seed")]
		public async Task<IActionResult> SeedTestData()
		{
			try
			{
				if (!await _weaviateService.ClassExistsAsync())
				{
					return BadRequest(new { error = "Classe DocumentChunk não existe. Execute o comando para criar a classe primeiro." });
				}

				var testDocuments = new[]
				{
					"Camisetas personalizadas custam entre R$ 25,00 e R$ 45,00 dependendo da quantidade e material.",
					"Canecas personalizadas têm prazo de entrega de 5 a 7 dias úteis e custam R$ 18,00 each.",
					"Para pedidos acima de 50 unidades, oferecemos 15% de desconto em todos os produtos.",
					"Trabalhamos com sublimação, serigrafia e bordado para personalização de produtos.",
					"Atendemos pedidos a partir de 10 unidades. Pedidos menores têm taxa adicional de R$ 10,00.",
					"Produtos em estoque têm entrega imediata. Produtos personalizados levam 3-7 dias.",
					"Aceitamos pagamento por PIX, cartão de crédito e débito. Parcelamos em até 6x sem juros.",
					"Fazemos orçamentos gratuitos. Entre em contato pelo WhatsApp (11) 99999-9999.",
					"Especialistas em eventos corporativos, formaturas e festas de aniversário.",
					"Garantia de 30 dias contra defeitos de fabricação em todos os produtos."
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
							errors.Add($"Documento {index} não retornou ID válido");
						}
					}
					catch (Exception ex)
					{
						errors.Add($"Erro no documento {index}: {ex.Message}");
					}
				}

				var result = new
				{
					message = $"Processo de seed concluído",
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
				return StatusCode(500, new { error = ex.Message, details = "Erro ao adicionar documentos de teste" });
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
					return BadRequest(new { error = "Classe DocumentChunk não existe. Não há dados para limpar." });
				}

				var countBefore = await _weaviateService.GetDocumentCountAsync();

				if (countBefore == 0)
				{
					return Ok(new { message = "Base de dados já está vazia.", documentsRemoved = 0 });
				}

				var removedCount = await _weaviateService.ClearAllDocumentsAsync();
				var countAfter = await _weaviateService.GetDocumentCountAsync();

				return Ok(new
				{
					message = "Dados limpos com sucesso",
					documentsRemoved = removedCount,
					before = countBefore,
					after = countAfter,
					timestamp = DateTime.UtcNow
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Erro ao limpar dados" });
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
					return BadRequest(new { error = "Classe DocumentChunk não existe." });
				}

				var removedCount = await _weaviateService.ClearDocumentsBySourcePrefixAsync("test_");

				return Ok(new
				{
					message = removedCount > 0 ? "Dados de teste removidos com sucesso" : "Nenhum dado de teste encontrado",
					documentsRemoved = removedCount,
					timestamp = DateTime.UtcNow
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Erro ao limpar dados de teste" });
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
					message = "Schema recriado com sucesso",
					details = result,
					timestamp = DateTime.UtcNow
				});
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Erro ao recriar schema" });
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
					return Ok(new { message = "Classe não existe", documentCount = 0 });
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
				return StatusCode(500, new { error = ex.Message, details = "Erro ao obter estatísticas" });
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
					return Ok(new { message = "Modelo já está disponível!" });
				}

				var result = await _llmService.PullModelAsync();
				return Ok(new { message = result });
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Erro ao baixar modelo" });
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
						results.Add("Classe DocumentChunk criada no Weaviate");
					}
					else
					{
						results.Add("Classe DocumentChunk já existe no Weaviate");
					}
				}
				catch (Exception ex)
				{
					errors.Add($"Erro ao criar classe Weaviate: {ex.Message}");
				}

				// 2. Verificar modelo LLM
				try
				{
					if (!await _llmService.IsModelAvailableAsync())
					{
						var pullResult = await _llmService.PullModelAsync();
						results.Add($"Modelo LLM baixado: {pullResult}");
					}
					else
					{
						results.Add("Modelo LLM já disponível");
					}
				}
				catch (Exception ex)
				{
					errors.Add($"Erro ao verificar/baixar modelo LLM: {ex.Message}");
				}

				var response = new
				{
					message = errors.Any() ? "Inicialização parcial" : "Sistema inicializado com sucesso",
					success = results,
					errors = errors.Any() ? errors : null,
					timestamp = DateTime.UtcNow
				};

				return errors.Any() ? StatusCode(207, response) : Ok(response); // 207 = Multi-Status
			}
			catch (Exception ex)
			{
				return StatusCode(500, new { error = ex.Message, details = "Erro na inicialização do sistema" });
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
 