using System.Text.RegularExpressions;

namespace TTG.SiteMap.Extensions
{
    public static class StringExtension
    {
        public static string NormalizeString(this string str, string spaceReplacement)
        {
            if (string.IsNullOrEmpty(str)) return "";
            var separatorRegex = new Regex($"[-/\\|]");
            var noSepStr = separatorRegex.Replace(str, " ");
            var nonArabicRegex = new Regex($"[^\u0621-\u064A\u0660-\u06690-9a-zA-Z ]");
            var arabicStr = nonArabicRegex.Replace(noSepStr.Trim(), "");

            var whiteSpaceRegex = new Regex(@"\s+", RegexOptions.Multiline);
            var withOutSpace = whiteSpaceRegex.Replace(arabicStr, spaceReplacement);
            return withOutSpace;
        }
    }
}