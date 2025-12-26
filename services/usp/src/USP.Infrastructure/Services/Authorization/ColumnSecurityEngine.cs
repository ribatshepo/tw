using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using USP.Core.Models.DTOs.Authorization;
using USP.Core.Services.Authorization;
using USP.Infrastructure.Data;

namespace USP.Infrastructure.Services.Authorization;

/// <summary>
/// Column-level security engine implementation
/// Provides fine-grained column access control, data masking, redaction, and tokenization
/// </summary>
public class ColumnSecurityEngine : IColumnSecurityEngine
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ColumnSecurityEngine> _logger;

    // In-memory rule storage (in production, this would be a database table)
    private static readonly List<ColumnSecurityRule> _columnRules = new();

    public ColumnSecurityEngine(
        ApplicationDbContext context,
        ILogger<ColumnSecurityEngine> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<ColumnAccessResponse> CheckColumnAccessAsync(ColumnAccessRequest request)
    {
        var response = new ColumnAccessResponse();

        try
        {
            _logger.LogInformation("Checking column access for user {UserId} on table {TableName}",
                request.UserId, request.TableName);

            // Get user roles
            var userRoles = await _context.UserRoles
                .Where(ur => ur.UserId == request.UserId)
                .Include(ur => ur.Role)
                .Select(ur => ur.Role.Name)
                .ToListAsync();

            // Get applicable rules for this table
            var rules = _columnRules
                .Where(r => r.IsActive &&
                           r.TableName.Equals(request.TableName, StringComparison.OrdinalIgnoreCase) &&
                           r.Operation.Equals(request.Operation, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.Priority)
                .ToList();

            foreach (var column in request.RequestedColumns)
            {
                var columnRules = rules.Where(r =>
                    r.ColumnName.Equals(column, StringComparison.OrdinalIgnoreCase) ||
                    r.ColumnName == "*").ToList();

                if (!columnRules.Any())
                {
                    // No rules defined - allow by default
                    response.AllowedColumns.Add(column);
                    continue;
                }

                var accessDecision = EvaluateColumnRules(columnRules, userRoles, column);

                switch (accessDecision.RestrictionType)
                {
                    case "allow":
                        response.AllowedColumns.Add(column);
                        break;

                    case "deny":
                        response.DeniedColumns.Add(column);
                        break;

                    case "mask":
                    case "redact":
                    case "tokenize":
                        response.AllowedColumns.Add(column);
                        response.ColumnRestrictions[column] = accessDecision.RestrictionType;
                        break;
                }
            }

            _logger.LogInformation("Column access check completed: {AllowedCount} allowed, {DeniedCount} denied, {RestrictedCount} restricted",
                response.AllowedColumns.Count, response.DeniedColumns.Count, response.ColumnRestrictions.Count);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking column access");
            return response;
        }
    }

    public async Task<Dictionary<string, object>> ApplyMaskingAsync(
        Guid userId,
        string tableName,
        Dictionary<string, object> data)
    {
        try
        {
            var maskedData = new Dictionary<string, object>(data);

            // Get column access response
            var accessRequest = new ColumnAccessRequest
            {
                UserId = userId,
                TableName = tableName,
                RequestedColumns = data.Keys.ToList(),
                Operation = "read"
            };

            var accessResponse = await CheckColumnAccessAsync(accessRequest);

            // Remove denied columns
            foreach (var deniedColumn in accessResponse.DeniedColumns)
            {
                maskedData.Remove(deniedColumn);
            }

            // Apply masking/redaction to restricted columns
            foreach (var (column, restrictionType) in accessResponse.ColumnRestrictions)
            {
                if (!maskedData.ContainsKey(column))
                    continue;

                var originalValue = maskedData[column];
                maskedData[column] = ApplyRestriction(originalValue, restrictionType, column, tableName);
            }

            return maskedData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying masking to data");
            return data;
        }
    }

    public Task<List<ColumnSecurityRule>> GetColumnRulesAsync(string tableName)
    {
        var rules = _columnRules
            .Where(r => r.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult(rules);
    }

    public Task<ColumnSecurityRule> CreateColumnRuleAsync(CreateColumnRuleRequest request)
    {
        var rule = new ColumnSecurityRule
        {
            Id = Guid.NewGuid(),
            TableName = request.TableName,
            ColumnName = request.ColumnName,
            Operation = request.Operation,
            RestrictionType = request.RestrictionType,
            MaskingPattern = request.MaskingPattern,
            AllowedRoles = request.AllowedRoles?.ToArray(),
            DeniedRoles = request.DeniedRoles?.ToArray(),
            Condition = request.Condition,
            Priority = request.Priority,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _columnRules.Add(rule);

        _logger.LogInformation("Created column security rule: {RuleId} for {TableName}.{ColumnName}",
            rule.Id, rule.TableName, rule.ColumnName);

        return Task.FromResult(rule);
    }

    public Task<bool> DeleteColumnRuleAsync(Guid ruleId)
    {
        var rule = _columnRules.FirstOrDefault(r => r.Id == ruleId);

        if (rule == null)
        {
            return Task.FromResult(false);
        }

        _columnRules.Remove(rule);

        _logger.LogInformation("Deleted column security rule: {RuleId}", ruleId);

        return Task.FromResult(true);
    }

    public async Task<List<string>> GetAllowedColumnsAsync(Guid userId, string tableName, string operation)
    {
        var userRoles = await _context.UserRoles
            .Where(ur => ur.UserId == userId)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        var rules = _columnRules
            .Where(r => r.IsActive &&
                       r.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase) &&
                       r.Operation.Equals(operation, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var allowedColumns = new List<string>();

        // Get all unique columns from rules
        var allColumns = rules.Select(r => r.ColumnName).Distinct().Where(c => c != "*").ToList();

        foreach (var column in allColumns)
        {
            var columnRules = rules.Where(r =>
                r.ColumnName.Equals(column, StringComparison.OrdinalIgnoreCase) ||
                r.ColumnName == "*").ToList();

            var decision = EvaluateColumnRules(columnRules, userRoles, column);

            if (decision.RestrictionType != "deny")
            {
                allowedColumns.Add(column);
            }
        }

        return allowedColumns;
    }

    #region Private Helper Methods

    private ColumnAccessDecision EvaluateColumnRules(List<ColumnSecurityRule> rules, List<string?> userRoles, string column)
    {
        // Default to allow if no rules
        if (!rules.Any())
        {
            return new ColumnAccessDecision { RestrictionType = "allow" };
        }

        // Process rules by priority (highest first)
        foreach (var rule in rules.OrderByDescending(r => r.Priority))
        {
            // Check denied roles first (explicit deny)
            if (rule.DeniedRoles != null && rule.DeniedRoles.Any())
            {
                if (userRoles.Any(ur => rule.DeniedRoles.Contains(ur, StringComparer.OrdinalIgnoreCase)))
                {
                    return new ColumnAccessDecision
                    {
                        RestrictionType = "deny",
                        Rule = rule
                    };
                }
            }

            // Check allowed roles
            if (rule.AllowedRoles != null && rule.AllowedRoles.Any())
            {
                if (userRoles.Any(ur => rule.AllowedRoles.Contains(ur, StringComparer.OrdinalIgnoreCase)))
                {
                    return new ColumnAccessDecision
                    {
                        RestrictionType = rule.RestrictionType,
                        Rule = rule,
                        MaskingPattern = rule.MaskingPattern
                    };
                }
            }
        }

        // No rules matched - deny by default for security
        return new ColumnAccessDecision { RestrictionType = "deny" };
    }

    private object ApplyRestriction(object value, string restrictionType, string column, string tableName)
    {
        if (value == null)
            return value;

        var stringValue = value.ToString() ?? string.Empty;

        return restrictionType.ToLowerInvariant() switch
        {
            "mask" => MaskValue(stringValue, column, tableName),
            "redact" => "[REDACTED]",
            "tokenize" => GenerateToken(stringValue, column),
            _ => value
        };
    }

    private string MaskValue(string value, string column, string tableName)
    {
        // Get masking pattern from rule
        var rule = _columnRules.FirstOrDefault(r =>
            r.TableName.Equals(tableName, StringComparison.OrdinalIgnoreCase) &&
            r.ColumnName.Equals(column, StringComparison.OrdinalIgnoreCase) &&
            r.RestrictionType == "mask");

        var pattern = rule?.MaskingPattern ?? "***";

        if (string.IsNullOrEmpty(value))
            return value;

        // Apply different masking strategies based on column name or pattern
        if (column.ToLowerInvariant().Contains("email"))
        {
            return MaskEmail(value);
        }
        else if (column.ToLowerInvariant().Contains("phone"))
        {
            return MaskPhone(value);
        }
        else if (column.ToLowerInvariant().Contains("ssn") || column.ToLowerInvariant().Contains("tax"))
        {
            return MaskSSN(value);
        }
        else if (column.ToLowerInvariant().Contains("credit") || column.ToLowerInvariant().Contains("card"))
        {
            return MaskCreditCard(value);
        }
        else
        {
            // Default masking - show first and last 2 characters
            if (value.Length <= 4)
                return pattern;

            return $"{value[..2]}{pattern}{value[^2..]}";
        }
    }

    private string MaskEmail(string email)
    {
        if (!email.Contains('@'))
            return "***@***.***";

        var parts = email.Split('@');
        var localPart = parts[0];
        var domain = parts[1];

        if (localPart.Length <= 2)
            return $"***@{domain}";

        return $"{localPart[0]}***{localPart[^1]}@{domain}";
    }

    private string MaskPhone(string phone)
    {
        // Remove non-digits
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.Length < 4)
            return "***-***-****";

        return $"***-***-{digits[^4..]}";
    }

    private string MaskSSN(string ssn)
    {
        var digits = new string(ssn.Where(char.IsDigit).ToArray());

        if (digits.Length < 4)
            return "***-**-****";

        return $"***-**-{digits[^4..]}";
    }

    private string MaskCreditCard(string card)
    {
        var digits = new string(card.Where(char.IsDigit).ToArray());

        if (digits.Length < 4)
            return "**** **** **** ****";

        return $"**** **** **** {digits[^4..]}";
    }

    private string GenerateToken(string value, string column)
    {
        // Generate deterministic token for consistency
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"{column}:{value}"));
        return $"TOK_{Convert.ToBase64String(hash)[..16]}";
    }

    #endregion
}

#region Helper Classes

internal class ColumnAccessDecision
{
    public string RestrictionType { get; set; } = "deny";
    public ColumnSecurityRule? Rule { get; set; }
    public string? MaskingPattern { get; set; }
}

#endregion
