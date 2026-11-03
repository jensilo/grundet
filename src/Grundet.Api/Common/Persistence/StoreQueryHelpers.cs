namespace Grundet.Api.Common.Persistence;

/// <summary>
/// Shared query-building/parsing helpers for the browse stores (requirements and work items),
/// which build the same paginated raw-SQL shape and parse the same string-typed columns.
/// </summary>
internal static class StoreQueryHelpers
{
    public static string EscapeLike(string value)
        => value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);

    public static int? TryParseReference(string? value, string prefix)
    {
        if (value is null || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string number = value[prefix.Length..];
        return int.TryParse(number, out int sequenceNumber) ? sequenceNumber : null;
    }

    public static TEnum ParseEnum<TEnum>(string value, string fieldName)
        where TEnum : struct, Enum
        => Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Could not parse '{fieldName}' enum value '{value}'.");
}
