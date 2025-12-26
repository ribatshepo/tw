using System.Text.Json.Serialization;

namespace USP.Core.Models.DTOs.SCIM;

/// <summary>
/// SCIM 2.0 Error Response (RFC 7644 Section 3.12)
/// </summary>
public class ScimError
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new List<string>
    {
        "urn:ietf:params:scim:api:messages:2.0:Error"
    };

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("scimType")]
    public string? ScimType { get; set; }

    [JsonPropertyName("detail")]
    public string? Detail { get; set; }
}
