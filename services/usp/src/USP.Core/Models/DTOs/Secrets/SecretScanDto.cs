namespace USP.Core.Models.DTOs.Secrets;

public class SecretScanResultDto
{
    public Guid ScanId { get; set; }
    public string RepositoryUrl { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string? CommitHash { get; set; }
    public int TotalFilesScanned { get; set; }
    public int SecretsFound { get; set; }
    public List<SecretFindingDto> Findings { get; set; } = new();
    public DateTime ScanStartedAt { get; set; }
    public DateTime ScanCompletedAt { get; set; }
    public TimeSpan ScanDuration { get; set; }
    public string Status { get; set; } = string.Empty; // completed, failed, in_progress
}

public class SecretFindingDto
{
    public Guid Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public int LineNumber { get; set; }
    public string SecretType { get; set; } = string.Empty; // api_key, password, certificate, private_key, token
    public string MatchedPattern { get; set; } = string.Empty;
    public string? MatchedValue { get; set; } // Redacted
    public int Confidence { get; set; } // 0-100
    public string Severity { get; set; } = string.Empty; // critical, high, medium, low
    public bool IsFalsePositive { get; set; }
    public string? RemediationStatus { get; set; } // pending, rotated, removed, ignored
    public DateTime FoundAt { get; set; }
}

public class CreateSecretScanRequest
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string? CommitHash { get; set; }
    public List<string>? FilePatterns { get; set; } // Glob patterns
    public List<string>? ExcludePatterns { get; set; }
    public bool AutoRemediate { get; set; }
}

public class SecretScannerRuleDto
{
    public Guid Id { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string SecretType { get; set; } = string.Empty;
    public string RegexPattern { get; set; } = string.Empty;
    public int Confidence { get; set; }
    public string Severity { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSecretScannerRuleRequest
{
    public string RuleName { get; set; } = string.Empty;
    public string SecretType { get; set; } = string.Empty;
    public string RegexPattern { get; set; } = string.Empty;
    public int Confidence { get; set; } = 80;
    public string Severity { get; set; } = "high";
    public bool IsEnabled { get; set; } = true;
}

public class MarkFalsePositiveRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class RemediateFindingRequest
{
    public string RemediationAction { get; set; } = string.Empty; // rotate, remove, ignore
    public string? Reason { get; set; }
}
