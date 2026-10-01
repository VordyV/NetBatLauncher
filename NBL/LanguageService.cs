using System.Globalization;

namespace NBL;

public enum AppLanguage
{
    Auto,
    Russian,
    English
}

public static class LanguageService
{
    public static AppLanguage SelectedLanguage { get; private set; } = AppLanguage.Auto;

    public static void SetLanguage(AppLanguage language)
    {
        SelectedLanguage = language;
    }

    public static AppLanguage GetCurrentLanguage()
    {
        if (SelectedLanguage != AppLanguage.Auto)
            return SelectedLanguage;

        return CultureInfo.InstalledUICulture.TwoLetterISOLanguageName
            .Equals("ru", StringComparison.OrdinalIgnoreCase)
            ? AppLanguage.Russian
            : AppLanguage.English;
    }

    public static string GetLanguageCode()
    {
        return GetCurrentLanguage() == AppLanguage.Russian
            ? "ru-RU"
            : "en-US";
    }
}