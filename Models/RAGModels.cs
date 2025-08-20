using MiniRAG.Api.Models;
using System.Collections.Generic;

namespace MiniRAG.Api.RAG.Models
{
	public class RAGRequest
	{
		public string Question { get; set; } = string.Empty;
		public int TopK { get; set; } = 5;
	}

	public class SearchRequest
	{
		public string Query { get; set; } = string.Empty;
		public int TopK { get; set; } = 10;
	}

	public class RAGResponse
	{
		public string Question { get; set; } = string.Empty;
		public string Answer { get; set; } = string.Empty;
		public List<Document> RelevantDocuments { get; set; } = new();
		public int DocumentCount { get; set; }
	}
}
