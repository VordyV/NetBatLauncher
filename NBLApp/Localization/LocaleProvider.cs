using System;
using System.ComponentModel;
using NBL.Services;

namespace NBLApp.Localization;

public sealed class LocaleProvider : INotifyPropertyChanged
{
    public event EventHandler? LanguageChanged;
    public static LocaleProvider Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    private LocaleProvider()
    {
        Locale.LanguageChanged += OnLanguageChanged;
    }

    public string this[string key]
    {
        get
        {
            return Locale.Get(key);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs("Item[]"));

        LanguageChanged?.Invoke(this, e);
    }
}
