namespace CPMS.DAL.Data;

internal static class PostgreSqlSearchPattern
{
    internal const string EscapeCharacter = "\\";

    internal static string Contains(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var escaped = value.Trim()
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
        return $"%{escaped}%";
    }
}
