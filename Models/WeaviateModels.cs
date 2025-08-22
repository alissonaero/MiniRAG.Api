using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MiniRAG.Api.Weaviate.Models
{
	public class WeaviateVectorSearchRequest
	{
		[JsonPropertyName("vector")]
		public float[] Vector { get; set; } = [];

		[JsonPropertyName("limit")]
		public int Limit { get; set; } = 10;

		[JsonPropertyName("class")]
		public string ClassName { get; set; } = string.Empty;

		[JsonPropertyName("fields")]
		public string[] Fields { get; set; } = [];

		[JsonPropertyName("certainty")]
		public float? Certainty { get; set; }
	}

 	public class WeaviateGraphQLResponse<T>
	{
		[JsonPropertyName("data")]
		public T? Data { get; set; }

		[JsonPropertyName("errors")]
		public WeaviateGraphQLError[]? Errors { get; set; }
	}

	public class WeaviateGraphQLError
	{
		[JsonPropertyName("message")]
		public string Message { get; set; } = string.Empty;

		[JsonPropertyName("path")]
		public string[]? Path { get; set; }
	}

	public class WeaviateGetData
	{
		[JsonPropertyName("Get")]
		public Dictionary<string, WeaviateDocumentResult[]> Get { get; set; } = new();
	}

	public class WeaviateAggregateData
	{
		[JsonPropertyName("Aggregate")]
		public Dictionary<string, WeaviateAggregateResult[]> Aggregate { get; set; } = new();
	}

	public class WeaviateDocumentResult
	{
		[JsonPropertyName("text")]
		public string? Text { get; set; }

		[JsonPropertyName("source")]
		public string? Source { get; set; }

		[JsonPropertyName("_additional")]
		public WeaviateAdditional? Additional { get; set; }
	}

	public class WeaviateAdditional
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[JsonPropertyName("distance")]
		public float? Distance { get; set; }

		[JsonPropertyName("certainty")]
		public float? Certainty { get; set; }
	}

	public class WeaviateAggregateResult
	{
		[JsonPropertyName("meta")]
		public WeaviateMeta? Meta { get; set; }
	}

	public class WeaviateMeta
	{
		[JsonPropertyName("count")]
		public int Count { get; set; }
	}

	public class WeaviateAddDocumentResponse
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[JsonPropertyName("class")]
		public string Class { get; set; } = string.Empty;

		[JsonPropertyName("properties")]
		public Dictionary<string, object> Properties { get; set; } = new();
	}

	// Modelos existentes mantidos
	public class WeaviateSearchResponse
	{
		[JsonPropertyName("results")]
		public WeaviateSearchResult[] Results { get; set; } = [];
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
		public WeaviateProperty[] Properties { get; set; } = [];
	}

	public class WeaviateProperty
	{
		[JsonPropertyName("name")]
		public string Name { get; set; } = string.Empty;

		[JsonPropertyName("dataType")]
		public string[] DataType { get; set; } = [];

		[JsonPropertyName("description")]
		public string Description { get; set; } = string.Empty;
	}

	public class WeaviateSchemaResponse
	{
		[JsonPropertyName("classes")]
		public WeaviateClassInfo[] Classes { get; set; } = [];
	}

	public class WeaviateClassInfo
	{
		[JsonPropertyName("class")]
		public string Class { get; set; } = string.Empty;

		[JsonPropertyName("description")]
		public string Description { get; set; } = string.Empty;
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
		public string[] Path { get; set; } = [];

		[JsonPropertyName("operator")]
		public string Operator { get; set; } = string.Empty;

		[JsonPropertyName("valueText")]
		public string ValueText { get; set; } = string.Empty;
	}

	public class WeaviateObjectsResponse
	{
		[JsonPropertyName("objects")]
		public WeaviateObject[] Objects { get; set; } = [];

		[JsonPropertyName("totalResults")]
		public int TotalResults { get; set; }
	}

	public class WeaviateObject
	{
		[JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[JsonPropertyName("properties")]
		public Dictionary<string, object> Properties { get; set; } = new();

		[JsonPropertyName("class")]
		public string Class { get; set; } = string.Empty;
	}
}