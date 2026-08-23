using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Brava.Domain;

/// <summary>
/// ADR-0002: lowercase, diacritics stripped, non-alphanumerics collapsed to a single hyphen.
/// Keep this in sync with the TypeScript slugify used by the Next.js frontend.
/// </summary>
public static partial class SlugGenerator
{
    public static string Generate(params string[] parts)
    {
        var input = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var withoutMarks = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                withoutMarks.Append(c);
            }
        }

        var ascii = withoutMarks.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        return NonAlphanumeric().Replace(ascii, "-").Trim('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
