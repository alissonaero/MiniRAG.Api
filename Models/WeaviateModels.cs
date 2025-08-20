using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;


namespace MiniRAG.Api.Weaviate.Models
{	
	public class WeaviateVectorSearchRequest
	{
		[JsonPropertyName("vector")]
		public float[] Vector { get; set; } = Array.Empty<float>();

		[JsonPropertyName("limit")]
		public int Limit { get; set; } = 10;

		[JsonPropertyName("class")]
		public string ClassName { get; set; } = string.Empty;

		[JsonPropertyName("fields")]
		public string[] Fields { get; set; } = Array.Empty<string>();

		[JsonPropertyName("certainty")]
		public float? Certainty { get; set; }
	}

	public class WeaviateSearchResponse
	{
		[JsonPropertyName("results")]
		public WeaviateSearchResult[] Results { get; set; } = Array.Empty<WeaviateSearchResult>();
	}

	public class WeaviateSearchResult
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[JsonPropertyName("text")]
		public string? Text { get; set; }

		[JsonPropertyName("source")]
		public string? Source { get; set; }

		[JsonPropertyName("distance")]
		public float? Distance { get; set; }

		[JsonPropertyName("certainty")]
		public float? Certainty { get; set; }
	}

	public class WeaviateClassSchema
	{
		[JsonPropertyName("class")]
		public string Class { get; set; } = string.Empty;

		[JsonPropertyName("description")]
		public string Description { get; set; } = string.Empty;

		[JsonPropertyName("vectorizer")]
		public string Vectorizer { get; set; } = "none";

		[JsonPropertyName("properties")]
		public WeaviateProperty[] Properties { get; set; } = Array.Empty<WeaviateProperty>();
	}

	public class WeaviateProperty
	{
		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("dataType")]
		public string[] DataType { get; set; } = Array.Empty<string>();

		[JsonPropertyName("description")]
		public string Description { get; set; } = string.Empty;
	}

	public class WeaviateSchemaResponse
	{
		[JsonPropertyName("classes")]
		public WeaviateClassInfo[] Classes { get; set; } = Array.Empty<WeaviateClassInfo>();
	}

	public class WeaviateClassInfo
	{
		[JsonPropertyName("class")]
		public string Class { get; set; } = string.Empty;

		[JsonPropertyName("description")]
		public string? Description { get; set; }
	}

	public class WeaviateBatchDeleteRequest
	{
		[JsonPropertyName("match")]
		public WeaviateDeleteMatch Match { get; set; } = new();

		[JsonPropertyName("output")]
		public string Output { get; set; } = "minimal";
	}

	public class WeaviateDeleteMatch
	{
		[JsonPropertyName("class")]
		public string Class { get; set; } = string.Empty;

		[JsonPropertyName("where")]
		public WeaviateWhereFilter? Where { get; set; }
	}

	public class WeaviateWhereFilter
	{
		[JsonPropertyName("path")]
		public string[] Path { get; set; } = Array.Empty<string>();

		[JsonPropertyName("operator")]
		public string Operator { get; set; } = string.Empty;

		[JsonPropertyName("valueText")]
		public string? ValueText { get; set; }
	}

	public class WeaviateObjectsResponse
	{
		[JsonPropertyName("objects")]
		public WeaviateObject[] Objects { get; set; } = Array.Empty<WeaviateObject>();

		[JsonPropertyName("totalResults")]
		public int TotalResults { get; set; }
	}

	public class WeaviateObject
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[JsonPropertyName("properties")]
		public Dictionary<string, object>? Properties { get; set; }

		[JsonPropertyName("class")]
		public string Class { get; set; } = string.Empty;
	}

}
