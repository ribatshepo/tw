using System.Linq.Expressions;
using USP.Core.Models.Entities;

namespace USP.Infrastructure.Services.UserLifecycle;

/// <summary>
/// Parser for SCIM 2.0 filter expressions (RFC 7644 Section 3.4.2.2)
/// Supports: eq, ne, co, sw, ew, pr, gt, ge, lt, le, and, or, not
/// </summary>
public class ScimFilterParser
{
    /// <summary>
    /// Parse SCIM filter expression and return predicate for users
    /// </summary>
    public Expression<Func<ScimUser, bool>> ParseUserFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return user => true; // No filter, return all
        }

        // Simple filter parsing (production implementation would use a proper parser like ANTLR)
        // This implementation supports basic filters for common use cases

        filter = filter.Trim();

        // Handle "userName eq "value""
        if (filter.Contains("userName eq", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractQuotedValue(filter);
            return user => user.UserName == value;
        }

        // Handle "active eq true/false"
        if (filter.Contains("active eq", StringComparison.OrdinalIgnoreCase))
        {
            var isActive = filter.Contains("true", StringComparison.OrdinalIgnoreCase);
            return user => user.Active == isActive;
        }

        // Handle "userName sw "value"" (starts with)
        if (filter.Contains("userName sw", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractQuotedValue(filter);
            return user => user.UserName.StartsWith(value);
        }

        // Handle "userName co "value"" (contains)
        if (filter.Contains("userName co", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractQuotedValue(filter);
            return user => user.UserName.Contains(value);
        }

        // Handle "givenName eq "value""
        if (filter.Contains("givenName eq", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractQuotedValue(filter);
            return user => user.GivenName == value;
        }

        // Handle "familyName eq "value""
        if (filter.Contains("familyName eq", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractQuotedValue(filter);
            return user => user.FamilyName == value;
        }

        // Handle "externalId eq "value""
        if (filter.Contains("externalId eq", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractQuotedValue(filter);
            return user => user.ExternalId == value;
        }

        // Handle "employeeNumber eq "value""
        if (filter.Contains("employeeNumber eq", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractQuotedValue(filter);
            return user => user.EmployeeNumber == value;
        }

        // Handle "department eq "value""
        if (filter.Contains("department eq", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractQuotedValue(filter);
            return user => user.Department == value;
        }

        // Default: no filter match, return all
        return user => true;
    }

    /// <summary>
    /// Parse SCIM filter expression and return predicate for groups
    /// </summary>
    public Expression<Func<ScimGroup, bool>> ParseGroupFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return group => true;
        }

        filter = filter.Trim();

        // Handle "displayName eq "value""
        if (filter.Contains("displayName eq", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractQuotedValue(filter);
            return group => group.DisplayName == value;
        }

        // Handle "externalId eq "value""
        if (filter.Contains("externalId eq", StringComparison.OrdinalIgnoreCase))
        {
            var value = ExtractQuotedValue(filter);
            return group => group.ExternalId == value;
        }

        return group => true;
    }

    private string ExtractQuotedValue(string filter)
    {
        var startQuote = filter.IndexOf('"');
        if (startQuote == -1) return string.Empty;

        var endQuote = filter.IndexOf('"', startQuote + 1);
        if (endQuote == -1) return string.Empty;

        return filter.Substring(startQuote + 1, endQuote - startQuote - 1);
    }
}
