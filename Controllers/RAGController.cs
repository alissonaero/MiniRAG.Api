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

		 
		//Full Pipeline | Question + Generated answer under the given context
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

		// Searchs for similar documents (no answer generation)
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


		// Checks all of the api services status
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

		//TODO: Create alternative methods for adding documents (files) in the next steps  
		//Adds some docs for testing and debugging porposes 
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
										// Página 1
										@"Cria Mineira
									Lembrancinhas e Presentes Personalizados
									Tabela de Preços
									Informações importantes
									- Pode haver variação de cor para o produto final.
									- Podem ocorrer mínimas variações de tamanho e montagem.
									- Leia as políticas de troca, devoluções e ressarcimentos.

									Valores válidos para um pedido mínimo de 20 unidades de CADA MODELO.
									Para quantidades menores, favor consultar!",

										@"Bloquinhos de Anotação
									Bloquinho Personalizado Encadernação tipo 'Livro' (sem arame)
									- Capa em papel triplex 300g
									- Tamanho: 10x7 cm, miolo 50 folhas
									Com mini-lápis, celofane, fita de cetim e tag: R$6,49

									Bloquinho Personalizado (wire-o branco)
									- Capa em papel triplex 300g
									- Tamanho: 10x7 cm, miolo 35 folhas
									Com mini-lápis, celofane, fita de cetim e tag: R$9,35",

										// Página 2
										@"Porcelanas Personalizadas (Caneca, Porta-Joia, Garrafinha, Vela)
									Limite de cores: sem limite (exceto ouro ou prata).
									Política de avarias e variações de tamanho descritas no documento.

									Mini Caneca 15ml: R$7,65
									Caneca de Café 50ml: R$13,20
									Imagem de Nossa Senhora (10cm):
									- Personalizada no manto + nome: R$34,90
									- Detalhes em ouro: +R$27,00
									- Nomes diferentes: +R$5,00/unidade",

										// Página 3 e 4
										@"Aparador de Jóias Oval (10x7cm)
									- Frente: R$23,20
									- Frente e verso: R$27,50
									- Filete Ouro/Prata: +R$18,00
									- Nomes diferentes: +R$5,00

									Aparador de Jóias Redondo Borda Lisa (8cm)
									- Frente: R$16,45
									- Frente e verso: R$20,75
									- Filete Ouro/Prata: +R$15,00
									- Nomes diferentes: +R$5,00",

										// Página 5 a 7
										@"Porta Joias 6cm
									- Tampa: R$17,98
									- Filete Ouro/Prata: +R$15,00
									- Lateral: +R$4,30
									- Fundo: +R$4,30
									- Caixinha acrílico: +R$6,80
									- Nomes diferentes: +R$5,00

									Porta Joias 8cm
									- Tampa: R$28,76
									- Filete Ouro/Prata: +R$19,90
									- Lateral: +R$6,00
									- Fundo: +R$6,00
									- Nomes diferentes: +R$5,00",

										@"Garrafinha Porcelana (6,5x5cm)
									- Frente: R$16,90
									- Frente e verso: R$21,20
									- Nomes diferentes: +R$5,00

									Vasos
									Manilha (11,5x6cm): R$38,98
									Funil (10x6,5cm): R$40,98
									+ Filete Ouro/Prata: +R$18,00
									+ Nomes diferentes: +R$5,00

									Vela no potinho porcelana (5x7cm)
									- Com vela frente: R$19,80
									- Com vela frente e verso: R$24,10
									- Só potinho frente: R$12,98
									- Só potinho frente e verso: R$17,28
									- Nomes diferentes: +R$5,00",

										// Página 8
										@"Toalhinha Personalizada (23x39cm, algodão)
									- Simples: R$7,98
									- + Fita de cetim e cartão: R$10,98
									- + Cofrinho: R$13,98

									Cofrinho personalizado (tampa branca): R$9,98

									Jogo da Memória (15 pares)
									- Papel cartão 250g
									- Papelão cinza rígido
									Preço sob consulta

									Quebra-cabeça personalizado (21x15cm, 12 peças)
									- Celofane + fita + tag: R$8,90
									- Latinha personalizada: R$14,90",

										// Página 9
										@"Adicionais
									- Tubolata personalizado + laço: R$13,00
									- Kit Organza + fita + tag: R$3,90
									- Kit Celofane + fita + tag: R$2,90
									- Caixinha acrílico porta-joias: R$6,90
									- Caixinha acetato (aparador): R$13,00
									- Caixinha papel cartão (caneca 50ml/porta-joias): R$5,90
									- Mini Terço: R$3,00",

										// Página 10 e 11
										@"Políticas da Loja
									- Pagamentos: Mercado Pago, PIX
									- Produção sob encomenda, prazo variável
									- Arte enviada em até 5 dias úteis após pagamento
									- 5 alterações inclusas, extras são cobradas
									- Após aprovação da arte não é possível cancelamento sem custo
									- Frete por conta do comprador (PAC, Sedex ou transportadora)
									- Trocas e devoluções apenas em caso de defeito
									- Reclamações de transporte só em até 7 dias do recebimento

									Contato
									WhatsApp: (37) 9-9988-5619
									Instagram: @criamineira
									Facebook: /CriaMineira
									Site: http://criamineira.com.br
									Data de Publicação: 20/01/2025"
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


		//Clears all documents in the Weaviate database
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

		//Removes test documents only (source must starts with "test_")
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


		//resets the schema (and data) by deleting and recreating the DocumentChunk class
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

	 
		//Gets weaviate db stats
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


		//Forces the LLM model to be downloaded for the first time
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

		//API Init (schema + LLM model)
		[HttpPost("initialize")]
		public async Task<IActionResult> Initialize()
		{
			try
			{
				var results = new List<string>();
				var errors = new List<string>();

				//check Weaviate class and create if not exists
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

				//check the LLM model  and download if not available
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

		 
		//just for checking if the API is running 		 
		[HttpGet("ping")]
		public IActionResult Ping()
		{
			return Ok(new { message = "MiniRAG API is running!", timestamp = DateTime.UtcNow });
		}
	}
}
 