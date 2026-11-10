
using System.ComponentModel;

namespace NBLApp.Localization;

public sealed class LocaleProvider : INotifyPropertyChanged
{
    public static LocaleProvider Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocaleProvider()
    {
        Locale.LanguageChanged += (_, _) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public string this[string key] => Locale.Get(key);
}
