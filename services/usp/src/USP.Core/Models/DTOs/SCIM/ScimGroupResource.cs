using System.Text.Json.Serialization;

namespace USP.Core.Models.DTOs.SCIM;

/// <summary>
/// SCIM 2.0 Group Resource (RFC 7644)
/// </summary>
public class ScimGroupResource
{
    [JsonPropertyName("schemas")]
    public List<string> Schemas { get; set; } = new List<string>
    {
        "urn:ietf:params:scim:schemas:core:2.0:Group"
    };

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("externalId")]
    public string? ExternalId { get; set; }

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("members")]
    public List<ScimGroupMemberRef>? Members { get; set; }

    [JsonPropertyName("meta")]
    public ScimMeta? Meta { get; set; }
}

public class ScimGroupMemberRef
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("$ref")]
    public string? Ref { get; set; }

    [JsonPropertyName("display")]
    public string? Display { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = "User";
}
