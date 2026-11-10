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
            Logger.Info(
                $"INDEXER: Key={key}, " +
                $"Culture={System.Globalization.CultureInfo.CurrentUICulture.Name}, " +
                $"Value={Locale.Get(key)}");

            return Locale.Get(key);
        }
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        Logger.Info("LocaleProvider: LanguageChanged received");

        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs("Item[]"));
        Logger.Info("LocaleProvider: Item[] notification sent");

        LanguageChanged?.Invoke(this, e);
    }
}
