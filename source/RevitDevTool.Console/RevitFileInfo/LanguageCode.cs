using System.Globalization;
using System.Reflection;

namespace RevitDevTool.Console.RevitFileInfo;

public class LanguageCode : IEquatable<LanguageCode>
{
    public static readonly LanguageCode ENU = new("ENU", "English_USA", CultureInfo.GetCultureInfo("en-US"));
    public static readonly LanguageCode ENG = new("ENG", "English_GB", CultureInfo.GetCultureInfo("en-GB"));
    public static readonly LanguageCode FRA = new("FRA", "French", CultureInfo.GetCultureInfo("fr-FR"));
    public static readonly LanguageCode DEU = new("DEU", "German", CultureInfo.GetCultureInfo("de-DE"));
    public static readonly LanguageCode ITA = new("ITA", "Italian", CultureInfo.GetCultureInfo("it-IT"));
    public static readonly LanguageCode JPN = new("JPN", "Japanese", CultureInfo.GetCultureInfo("ja-JP"));
    public static readonly LanguageCode KOR = new("KOR", "Korean", CultureInfo.GetCultureInfo("ko-KR"));
    public static readonly LanguageCode PLK = new("PLK", "Polish", CultureInfo.GetCultureInfo("pl-PL"));
    public static readonly LanguageCode ESP = new("ESP", "Spanish", CultureInfo.GetCultureInfo("es"));
    public static readonly LanguageCode CHS = new("CHS", "Chinese_Simplified", CultureInfo.GetCultureInfo("zh-CN"));
    public static readonly LanguageCode CHT = new("CHT", "Chinese_Traditional", CultureInfo.GetCultureInfo("zh-Hant"));
    public static readonly LanguageCode PTB = new("PTB", "Brazilian_Portuguese", CultureInfo.GetCultureInfo("pt-BR"));
    public static readonly LanguageCode RUS = new("RUS", "Russian", CultureInfo.GetCultureInfo("ru-RU"));
    public static readonly LanguageCode CSY = new("CSY", "Czech", CultureInfo.GetCultureInfo("cs-CZ"));
    public static readonly LanguageCode HUN = new("HUN", "Hungarian", CultureInfo.GetCultureInfo("hu-HU"));
    public static readonly LanguageCode Unknown = new("Unknown", "Unknown", CultureInfo.CurrentCulture);

    private LanguageCode(string code, string fullCode, CultureInfo cultureInfo)
    {
        Code = code;
        FullCode = fullCode;
        CultureInfo = cultureInfo;
    }

    public string Code { get; }
    public string FullCode { get; }
    public CultureInfo CultureInfo { get; }

    public static LanguageCode GetLanguageCode(string languageCode)
    {
        if (string.IsNullOrEmpty(languageCode))
            return Unknown;

        return GetLanguageCodes()
                   .FirstOrDefault(item =>
                       languageCode.Equals(item.Code, StringComparison.CurrentCultureIgnoreCase)
                       || languageCode.Equals(item.FullCode, StringComparison.CurrentCultureIgnoreCase))
               ?? Unknown;
    }

    private static IEnumerable<LanguageCode> GetLanguageCodes()
    {
        return typeof(LanguageCode)
            .GetFields(BindingFlags.Static | BindingFlags.Public)
            .Select(item => item.GetValue(null))
            .OfType<LanguageCode>();
    }

    public bool Equals(LanguageCode? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Code, other.Code, StringComparison.CurrentCulture)
               && string.Equals(FullCode, other.FullCode, StringComparison.CurrentCulture);
    }

    public override bool Equals(object? obj) => obj is LanguageCode other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Code, FullCode);
    public override string ToString() => CultureInfo.DisplayName;
}
