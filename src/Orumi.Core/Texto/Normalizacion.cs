using System.Globalization;
using System.Text;

namespace Orumi.Core.Texto;

public static class Normalizacion
{
    /// <summary>Minúsculas y sin acentos, para comparar nombres («Gullón», «gullon» y «GULLÓN» coinciden).</summary>
    public static string SinAcentos(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        {
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
