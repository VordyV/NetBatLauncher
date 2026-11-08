
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Irihi.Avalonia.Shared.Contracts;
using NBL;
using NBL.Services;
using NBLApp.Localization;
using System;
using System.Collections.Generic;
using System.Linq;
using Ursa.Controls;

namespace NBLApp.Forms;

public partial class LaunchSettings : UserControl
{
    protected Game Game;
    protected ILaunchParam[] LaunchParams;
    protected Dictionary<string, Control> Controls = new();
    private readonly IntroVideoService IntroVideoService = new();
    private readonly List<(TextBlock Label, ILaunchParam Param)> Labels = new();
    private readonly List<(Control Control, ILaunchParam Param)> ToolTips = new();
    private bool subscribed;

    public LaunchSettings()
    {
        InitializeComponent();
        DataContext = this;
        SubscribeLanguage();
    }

    public LaunchSettings(Game game)
    {
        Game = game;
        LaunchParams = game.LaunchParams.Values.ToArray();

        InitializeComponent();
        DataContext = this;

        try
        {
            CheckBoxNoIntro.IsChecked = IntroVideoService.AreIntrosDisabled(Game.GetPath());
            TextBlockNoIntroStatus.Text = " ";
        }
        catch (GamePathNotSetException)
        {
            DisableIntroOption();
        }
        catch (GamePathNotFoundException)
        {
            DisableIntroOption();
        }

        Dictionary<string, object> savedParameters = Game.GetLaunchParameters();

        foreach (ILaunchParam param in LaunchParams)
        {
            Control control;

            if (param is LaunchParamBool)
            {
                bool value = savedParameters.TryGetValue(param.Id, out object? savedValue)
                    && savedValue is bool boolValue ? boolValue : param.Id == "widescreen";

                control = new CheckBox { IsChecked = value };
            }
            else if (param is LaunchParamDict p)
            {
                List<ComboBoxItem> items = p.Dictionary.Keys
                    .Select(key => new ComboBoxItem { Content = key })
                    .ToList();

                string selectedValue = savedParameters.TryGetValue(param.Id, out object? savedValue)
                    && savedValue is string stringValue
                        ? stringValue
                        : p.Dictionary.Keys.FirstOrDefault() ?? "";

                control = new ComboBox
                {
                    ItemsSource = items,
                    SelectedIndex = Math.Max(0, p.Dictionary.Keys.ToList().IndexOf(selectedValue)),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
            }
            else if (param is LaunchOptionDict options)
            {
                StackPanel panel = new() { Spacing = 6 };

                string[] selectedValues = savedParameters.TryGetValue(param.Id, out object? savedValue)
                    && savedValue is string[] values ? values : Array.Empty<string>();

                foreach (string key in options.Dictionary.Keys)
                    panel.Children.Add(new CheckBox { Content = key, IsChecked = selectedValues.Contains(key) });

                control = panel;
            }
            else if (param is LaunchParamCustom)
            {
                string value = savedParameters.TryGetValue(param.Id, out object? savedValue)
                    && savedValue is string stringValue ? stringValue : "";

                StackPanel panel = new() { Spacing = 6 };
                panel.Children.Add(new TextBox
                {
                    PlaceholderText = "...+nosound 1...+widescreen 1...",
                    Text = value,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                });

                control = panel;
            }
            else
                throw new InvalidOperationException($"Launch parameter '{param.Name}' type not specified");

            Controls.Add(param.Id, control);

            StackPanel label = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
            TextBlock title = new() { Text = GetName(param), VerticalAlignment = VerticalAlignment.Center };
            label.Children.Add(title);
            Labels.Add((title, param));

            if (!string.IsNullOrWhiteSpace(param.Description)
                && Application.Current!.Styles.TryGetResource("SemiIconHelpCircle", ThemeVariant.Default, out var resource)
                && resource is Geometry geometry)
            {
                PathIcon icon = new()
                {
                    Data = geometry,
                    Width = 16,
                    Height = 16,
                    Foreground = new SolidColorBrush(Color.Parse("#777777"))
                };

                ToolTip.SetTip(icon, GetDescription(param));
                ToolTips.Add((icon, param));
                label.Children.Add(icon);
            }

            if (control is CheckBox checkBox)
                checkBox.Content = label;
            else
                FormItem.SetLabel(control, label);

            MainContent.Items.Add(control);
        }

        SubscribeLanguage();
    }

    private static string GetName(ILaunchParam param) =>
        param.NameKey is string key ? Locale.Get(key) : param.Name;

    private static string? GetDescription(ILaunchParam param) =>
        param.DescriptionKey is string key ? Locale.Get(key) : param.Description;

    private void DisableIntroOption()
    {
        CheckBoxNoIntro.IsChecked = false;
        CheckBoxNoIntro.IsEnabled = false;
        TextBlockNoIntroStatus.Text = Locale.Get("LaunchSettingsGameNotInstalled");
    }

    private void SubscribeLanguage()
    {
        if (subscribed)
            return;

        LocaleProvider.Instance.LanguageChanged += OnLanguageChanged;
        subscribed = true;
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        foreach (var (label, param) in Labels)
            label.Text = GetName(param);

        foreach (var (control, param) in ToolTips)
            ToolTip.SetTip(control, GetDescription(param));

        CheckBoxNoIntro.Content = Locale.Get("LaunchSettingsNoIntro");

        if (!CheckBoxNoIntro.IsEnabled)
            TextBlockNoIntroStatus.Text = Locale.Get("LaunchSettingsGameNotInstalled");

        if (ButtonCancel.Content is not null)
            ButtonCancel.Content = Locale.Get("Cancel");

        if (ButtonSave.Content is not null)
            ButtonSave.Content = Locale.Get("Save");

        LaunchSettingsForm.Title = Locale.Get("LaunchSettingsTitle");

    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        LocaleProvider.Instance.LanguageChanged -= OnLanguageChanged;
        subscribed = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IDialogContext ctx)
            ctx.Close();
    }

    private void ButtonSave_OnClick(object? sender, RoutedEventArgs e)
    {
        Dictionary<string, object> parameters = new();

        foreach (ILaunchParam launchParam in LaunchParams)
        {
            if (!Controls.TryGetValue(launchParam.Id, out Control? control))
                continue;

            if (launchParam is LaunchParamBool)
                parameters[launchParam.Id] = ((CheckBox)control).IsChecked ?? false;
            else if (launchParam is LaunchParamDict)
            {
                if (((ComboBox)control).SelectedItem is ComboBoxItem item)
                    parameters[launchParam.Id] = item.Content?.ToString() ?? "";
            }
            else if (launchParam is LaunchOptionDict)
            {
                List<string> selectedOptions = ((StackPanel)control).Children
                    .OfType<CheckBox>()
                    .Where(checkBox => checkBox.IsChecked == true && checkBox.Content is string)
                    .Select(checkBox => checkBox.Content!.ToString()!)
                    .ToList();

                parameters[launchParam.Id] = selectedOptions.ToArray();
            }
            else if (launchParam is LaunchParamCustom)
            {
                TextBox? textBox = ((StackPanel)control).Children.OfType<TextBox>().FirstOrDefault();
                parameters[launchParam.Id] = textBox?.Text ?? "";
            }
        }

        try
        {
            Game.SetLaunchParameters(parameters);

            try
            {
                IntroVideoService.SetIntrosDisabled(Game.GetPath(), CheckBoxNoIntro.IsChecked ?? false);
            }
            catch (GamePathNotSetException) { }
            catch (GamePathNotFoundException) { }

            if (DataContext is IDialogContext ctx)
                ctx.Close();
        }
        catch (Exception exception)
        {
            Console.WriteLine(exception);
        }
    }
}
