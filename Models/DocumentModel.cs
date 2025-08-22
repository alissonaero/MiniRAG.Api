namespace MiniRAG.Api.Models
{
	public class Document
	{
		public string Id { get; set; }
		public string Texto { get; set; }
		public float[] Embedding { get; set; }
		public string Source { get; internal set; }
	}

 
}
