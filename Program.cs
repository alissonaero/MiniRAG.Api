using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using MiniRAG.Api.Services.Embedding;
using MiniRAG.Api.Services.LLama;
using MiniRAG.Api.Services.Weaviate;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient<EmbeddingService>(); 
builder.Services.AddHttpClient<WeaviateClient>();
builder.Services.AddHttpClient<LLMService>();
builder.Services.AddScoped<IWeaviateService, WeaviateService>();

var app = builder.Build();
app.MapControllers();
app.Run();
