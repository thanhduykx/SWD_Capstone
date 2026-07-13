using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CPMS.Core.Exceptions;

namespace CPMS.Core.Services;

public static partial class AccountUsernameGenerator
{
    public static string Generate(string fullName, string identityCode)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new BusinessRuleException("Full name is required for account username generation.");
        }

        if (string.IsNullOrWhiteSpace(identityCode))
        {
            throw new BusinessRuleException("Identity code is required for account username generation.");
        }

        var nameParts = Spaces()
            .Split(RemoveDiacritics(fullName).Trim())
            .Select(KeepLettersAndDigits)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        if (nameParts.Length == 0)
        {
            throw new BusinessRuleException("Full name must contain at least one valid letter or digit.");
        }

        var givenName = ToPascalWord(nameParts[^1]);
        var initials = string.Concat(nameParts
            .Take(nameParts.Length - 1)
            .Select(part => char.ToUpperInvariant(part[0])));
        var normalizedCode = KeepLettersAndDigits(RemoveDiacritics(identityCode)).ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new BusinessRuleException("Identity code must contain at least one valid letter or digit.");
        }

        return $"{givenName}{initials}{normalizedCode}";
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(capacity: normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character switch
                {
                    'Đ' => 'D',
                    'đ' => 'd',
                    _ => character
                });
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private static string KeepLettersAndDigits(string value) =>
        LettersAndDigits().Replace(value, string.Empty);

    private static string ToPascalWord(string value)
    {
        var lower = value.ToLowerInvariant();
        return $"{char.ToUpperInvariant(lower[0])}{lower[1..]}";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();

    [GeneratedRegex(@"[^A-Za-z0-9]")]
    private static partial Regex LettersAndDigits();
}
