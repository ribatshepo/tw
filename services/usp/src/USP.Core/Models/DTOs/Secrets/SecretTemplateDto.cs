namespace USP.Core.Models.DTOs.Secrets;

public class SecretTemplateDto
{
    public Guid Id { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty; // database, api, cloud, custom
    public Dictionary<string, SecretTemplateFieldDto> Fields { get; set; } = new();
    public Dictionary<string, string>? DefaultValues { get; set; }
    public bool IsBuiltIn { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SecretTemplateFieldDto
{
    public string FieldName { get; set; } = string.Empty;
    public string FieldType { get; set; } = string.Empty; // string, password, url, json
    public bool IsRequired { get; set; }
    public string? ValidationRegex { get; set; }
    public string? ValidationMessage { get; set; }
    public string? DefaultValue { get; set; }
    public string? Description { get; set; }
    public int? MaxLength { get; set; }
    public int? MinLength { get; set; }
}

public class CreateSecretTemplateRequest
{
    public string TemplateName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "custom";
    public Dictionary<string, SecretTemplateFieldDto> Fields { get; set; } = new();
    public Dictionary<string, string>? DefaultValues { get; set; }
}

public class CreateSecretFromTemplateRequest
{
    public Guid TemplateId { get; set; }
    public string SecretPath { get; set; } = string.Empty;
    public Dictionary<string, string> FieldValues { get; set; } = new();
}

public class SecretTemplateValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, string>? FieldErrors { get; set; }
}
