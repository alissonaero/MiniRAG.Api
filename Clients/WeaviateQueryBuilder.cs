using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace MiniRAG.Api.Clients.Weaviate;

public class WeaviateQueryBuilder
{
	private readonly StringBuilder _query = new();
	private string _className = string.Empty;
	private readonly List<string> _fields = [];
	private readonly List<string> _additionalFields = [];
	private string? _whereClause;
	private string? _nearVectorClause;
	private int? _limit;
	private QueryType _queryType = QueryType.Get;

	public enum QueryType
	{
		Get,
		Aggregate
	}

	public static WeaviateQueryBuilder Create() => new();

	public WeaviateQueryBuilder Get(string className)
	{
		_queryType = QueryType.Get;
		_className = className;
		return this;
	}

	public WeaviateQueryBuilder Aggregate(string className)
	{
		_queryType = QueryType.Aggregate;
		_className = className;
		return this;
	}

	public WeaviateQueryBuilder WithFields(params string[] fields)
	{
		_fields.AddRange(fields);
		return this;
	}

	public WeaviateQueryBuilder WithAdditional(params string[] additionalFields)
	{
		_additionalFields.AddRange(additionalFields);
		return this;
	}

	public WeaviateQueryBuilder NearVector(float[] vector)
	{
		var vectorStr = string.Join(",", vector.Select(f => f.ToString("G", CultureInfo.InvariantCulture)));
		_nearVectorClause = $"nearVector: {{ vector: [{vectorStr}] }}";
		return this;
	}

	public WeaviateQueryBuilder Where(string path, string op, string value)
	{
		_whereClause = $"where: {{ path: [\"{path}\"], operator: {op}, valueText: \"{value}\" }}";
		return this;
	}

	public WeaviateQueryBuilder WhereLike(string path, string pattern)
	{
		return Where(path, "Like", pattern);
	}

	public WeaviateQueryBuilder WhereEqual(string path, string value)
	{
		return Where(path, "Equal", value);
	}

	public WeaviateQueryBuilder Limit(int limit)
	{
		_limit = limit;
		return this;
	}

	public WeaviateQueryBuilder WithMeta()
	{
		if (_queryType == QueryType.Aggregate)
		{
			_fields.Add("meta { count }");
		}
		return this;
	}

	public string Build()
	{
		ValidateQuery();

		var query = new StringBuilder();
		query.AppendLine("{");

		if (_queryType == QueryType.Get)
		{
			BuildGetQuery(query);
		}
		else
		{
			BuildAggregateQuery(query);
		}

		query.AppendLine("}");

		return query.ToString().Replace("\n", "").Replace("\t", "");
	}

	private void BuildGetQuery(StringBuilder query)
	{
		query.AppendLine($"  Get {{");
		query.Append($"    {_className}(");

		var parameters = new List<string>();

		if (!string.IsNullOrEmpty(_nearVectorClause))
			parameters.Add(_nearVectorClause);

		if (!string.IsNullOrEmpty(_whereClause))
			parameters.Add(_whereClause);

		if (_limit.HasValue)
			parameters.Add($"limit: {_limit}");

		query.Append(string.Join(", ", parameters));
		query.AppendLine(") {");

		//Adds fields
		foreach (var field in _fields)
		{
			query.AppendLine($"      {field}");
		}

		//Adds _additional if any
		if (_additionalFields.Any())
		{
			query.AppendLine("      _additional {");
			foreach (var additionalField in _additionalFields)
			{
				query.AppendLine($"        {additionalField}");
			}
			query.AppendLine("      }");
		}

		query.AppendLine("    }");
		query.AppendLine("  }");
	}

	private void BuildAggregateQuery(StringBuilder query)
	{
		query.AppendLine($"  Aggregate {{");
		query.Append($"    {_className}(");

		var parameters = new List<string>();

		if (!string.IsNullOrEmpty(_whereClause))
			parameters.Add(_whereClause);

		query.Append(string.Join(", ", parameters));
		query.AppendLine(") {");

		// Para Aggregate, normalmente queremos meta
		foreach (var field in _fields)
		{
			query.AppendLine($"      {field}");
		}

		query.AppendLine("    }");
		query.AppendLine("  }");
	}

	private void ValidateQuery()
	{
		if (string.IsNullOrEmpty(_className))
			throw new InvalidOperationException("ClassName is required. Use Get() or Aggregate() method.");

		if (_queryType == QueryType.Get && !_fields.Any() && !_additionalFields.Any())
			throw new InvalidOperationException("At least one field must be specified for Get queries.");
	}
}

//Extension methods to let it even more fluent =:^)
public static class WeaviateQueryBuilderExtensions
{
	public static WeaviateQueryBuilder SearchDocuments(this WeaviateQueryBuilder builder, string className)
	{
		return builder
			.Get(className)
			.WithFields("text", "source")
			.WithAdditional("id", "distance", "certainty");
	}

	public static WeaviateQueryBuilder CountDocuments(this WeaviateQueryBuilder builder, string className)
	{
		return builder
			.Aggregate(className)
			.WithMeta();
	}

	public static WeaviateQueryBuilder GetAllDocuments(this WeaviateQueryBuilder builder, string className)
	{
		return builder
			.Get(className)
			.WithFields("text", "source")
			.WithAdditional("id")
			.Limit(10000);
	}
}
