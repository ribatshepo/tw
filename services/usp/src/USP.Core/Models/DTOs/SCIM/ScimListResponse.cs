using System.Text.Json.Serialization;

namespace USP.Core.Models.DTOs.SCIM;

/// <summary>
/// SCIM 2.0 List Response for paginated results
/// </summary>
public class ScimListResponse<T>
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new List<string>
    {
        "urn:ietf:params:scim:api:messages:2.0:ListResponse"
    };

    [JsonPropertyName("totalResults")]
    public int TotalResults { get; set; }

    [JsonPropertyName("Resources")]
    public List<T> Resources { get; set; } = new List<T>();

    [JsonPropertyName("startIndex")]
    public int StartIndex { get; set; } = 1;

    [JsonPropertyName("itemsPerPage")]
    public int ItemsPerPage { get; set; }
}
