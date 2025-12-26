namespace USP.Core.Models.Configuration;

/// <summary>
/// API threat protection configuration settings
/// </summary>
public class ApiThreatProtectionSettings
{
    /// <summary>
    /// Enable SQL injection detection
    /// </summary>
    public bool EnableSqlInjectionDetection { get; set; } = true;

    /// <summary>
    /// Enable XSS (Cross-Site Scripting) detection
    /// </summary>
    public bool EnableXssDetection { get; set; } = true;

    /// <summary>
    /// Enable path traversal detection
    /// </summary>
    public bool EnablePathTraversalDetection { get; set; } = true;

    /// <summary>
    /// Enable JSON depth limiting
    /// </summary>
    public bool EnableJsonDepthLimiting { get; set; } = true;

    /// <summary>
    /// Enable request header validation
    /// </summary>
    public bool EnableHeaderValidation { get; set; } = true;

    /// <summary>
    /// Enable suspicious pattern detection (rapid sequential requests)
    /// </summary>
    public bool EnableSuspiciousPatternDetection { get; set; } = true;

    /// <summary>
    /// Maximum allowed JSON depth
    /// </summary>
    public int MaxJsonDepth { get; set; } = 10;

    /// <summary>
    /// Maximum request body size in bytes (4MB default)
    /// </summary>
    public long MaxRequestBodySize { get; set; } = 4 * 1024 * 1024;

    /// <summary>
    /// Maximum header size in bytes
    /// </summary>
    public int MaxHeaderSize { get; set; } = 8 * 1024;

    /// <summary>
    /// Maximum number of headers
    /// </summary>
    public int MaxHeaderCount { get; set; } = 100;

    /// <summary>
    /// Suspicious rapid request threshold (requests per second from same IP)
    /// </summary>
    public int RapidRequestThreshold { get; set; } = 50;

    /// <summary>
    /// Time window in seconds for rapid request detection
    /// </summary>
    public int RapidRequestWindowSeconds { get; set; } = 1;

    /// <summary>
    /// Block request when threat is detected (if false, only log)
    /// </summary>
    public bool BlockOnThreatDetection { get; set; } = true;

    /// <summary>
    /// SQL injection detection patterns (regex)
    /// </summary>
    public List<string> SqlInjectionPatterns { get; set; } = new()
    {
        @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION|DECLARE)\b)",
        @"(--|#|/\*|\*/)",
        @"(\bOR\b\s+\d+\s*=\s*\d+)",
        @"(\bAND\b\s+\d+\s*=\s*\d+)",
        @"('|""|;|--)",
        @"(\bxp_|\bsp_)"
    };

    /// <summary>
    /// XSS detection patterns (regex)
    /// </summary>
    public List<string> XssPatterns { get; set; } = new()
    {
        @"<script[^>]*>.*?</script>",
        @"javascript:",
        @"onerror\s*=",
        @"onload\s*=",
        @"onclick\s*=",
        @"<iframe[^>]*>",
        @"<object[^>]*>",
        @"<embed[^>]*>",
        @"eval\s*\(",
        @"document\.cookie",
        @"document\.write"
    };

    /// <summary>
    /// Path traversal detection patterns (regex)
    /// </summary>
    public List<string> PathTraversalPatterns { get; set; } = new()
    {
        @"\.\./",
        @"\.\.\\/",
        @"%2e%2e/",
        @"%2e%2e\\",
        @"\.\.\\",
        @"%252e%252e/",
        @"..;/"
    };

    /// <summary>
    /// Validates API threat protection configuration settings
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when validation fails</exception>
    public void Validate()
    {
        if (MaxJsonDepth <= 0)
        {
            throw new InvalidOperationException("Max JSON depth must be positive");
        }

        if (MaxRequestBodySize <= 0)
        {
            throw new InvalidOperationException("Max request body size must be positive");
        }

        if (MaxHeaderSize <= 0)
        {
            throw new InvalidOperationException("Max header size must be positive");
        }

        if (MaxHeaderCount <= 0)
        {
            throw new InvalidOperationException("Max header count must be positive");
        }

        if (RapidRequestThreshold <= 0)
        {
            throw new InvalidOperationException("Rapid request threshold must be positive");
        }

        if (RapidRequestWindowSeconds <= 0)
        {
            throw new InvalidOperationException("Rapid request window seconds must be positive");
        }
    }
}
