using System.Text.Json;
using System.Web;

namespace MiniRAG.Api.Core.Helpers.Weaviate
{
	public static class WeaviateHelpers
	{
		public static string BuildWeaviateFilter(string path, string op, string value)
		{
			var filter = new
			{
				path = new[] { path },
				@operator = op,
				valueText = value
			};

			var json = JsonSerializer.Serialize(filter);
			return HttpUtility.UrlEncode(json);
		}

	}
}
