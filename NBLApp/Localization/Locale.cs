using System;
using System.Globalization;
using System.Resources;

namespace NBLApp.Localization;

public static class Locale
{
    private static readonly ResourceManager ResourceManager =
        new(
            "NBLApp.Localization.Locale",
            typeof(Locale).Assembly);

    public static event EventHandler? LanguageChanged;

    public static string Get(string key)
    {
        return ResourceManager.GetString(
            key,
            CultureInfo.CurrentUICulture)
            ?? key;
    }

    public static void SetLanguage(string language)
    {
        CultureInfo culture;

        if (language == "auto")
        {
            culture =
                CultureInfo.InstalledUICulture.Name.StartsWith("ru")
                    ? new CultureInfo("ru-RU")
                    : new CultureInfo("en-US");
        }
        else
        {
            culture = new CultureInfo(language);
        }

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }
}