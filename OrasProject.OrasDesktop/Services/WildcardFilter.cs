using System;
using System.Text.RegularExpressions;

namespace OrasProject.OrasDesktop.Services;

/// <summary>
/// Provides wildcard filtering for strings using * as a wildcard character.
/// Supports patterns like "prefix*", "*suffix", "*middle*", and combinations.
/// </summary>
public static class WildcardFilter
{
    /// <summary>
    /// Checks if a value matches the given filter pattern.
    /// </summary>
    /// <param name="value">The value to check</param>
    /// <param name="pattern">The filter pattern (supports * wildcard, case-insensitive)</param>
    /// <returns>True if the value matches the pattern, false otherwise</returns>
    public static bool Matches(string value, string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return true; // Empty pattern matches everything
        }

        if (string.IsNullOrEmpty(value))
        {
            return false; // Empty value only matches empty pattern
        }

        // If no wildcard, do simple case-insensitive contains check
        if (!pattern.Contains('*'))
        {
            return value.Contains(pattern, StringComparison.OrdinalIgnoreCase);
        }

        // Convert wildcard pattern to regex
        // Escape regex special characters except *
        string regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        
        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Checks if a pattern contains wildcard characters.
    /// </summary>
    public static bool HasWildcard(string pattern)
    {
        return !string.IsNullOrEmpty(pattern) && pattern.Contains('*');
    }

    /// <summary>
    /// Creates a display-friendly description of what the filter does.
    /// </summary>
    public static string GetFilterDescription(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return "showing all items";
        }

        if (!HasWildcard(pattern))
        {
            return $"containing '{pattern}'";
        }

        if (pattern == "*")
        {
            return "showing all items";
        }

        if (pattern.StartsWith("*") && pattern.EndsWith("*") && pattern.Length > 2)
        {
            return $"containing '{pattern.Trim('*')}'";
        }

        if (pattern.StartsWith("*"))
        {
            return $"ending with '{pattern.TrimStart('*')}'";
        }

        if (pattern.EndsWith("*"))
        {
            return $"starting with '{pattern.TrimEnd('*')}'";
        }

        return $"matching '{pattern}'";
    }
}
