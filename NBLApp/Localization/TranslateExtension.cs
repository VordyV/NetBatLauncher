
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using System;

namespace NBLApp.Localization;

public class TranslateExtension : MarkupExtension
{
    public string Key { get; }

    public TranslateExtension(string key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding($"[{Key}]")
        {
            Source = LocaleProvider.Instance,
            Mode = BindingMode.OneWay
        };
    }
}
