
using System;
using System.Globalization;
using System.Resources;

namespace NBLApp.Localization;

public static class Locale
{
    private static readonly ResourceManager ResourceManager = new(
        "NBLApp.Localization.Locale",
        typeof(Locale).Assembly);

    public static event EventHandler? LanguageChanged;

    public static string Get(string key)
    {
        return ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }

    public static void SetLanguage(string language)
    {
        CultureInfo culture = language == "auto"
            ? new CultureInfo(CultureInfo.InstalledUICulture.Name.StartsWith("ru") ? "ru-RU" : "en-US")
            : new CultureInfo(language);

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }
}
