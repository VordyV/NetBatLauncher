
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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
        UpdateShortcutButton();
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
        ButtonShortcut.Content = Locale.Get("LaunchSettingsCreateShortcut");

        UpdateShortcutButton();

    }
    private string GetShortcutPath()
    {
        string desktopPath = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory);

        return Path.Combine(desktopPath, "Battlefield 2142.lnk");
    }

    private void UpdateShortcutButton()
    {
        if (ButtonShortcut == null)
            return;

        ButtonShortcut.Content = Locale.Get(
            File.Exists(GetShortcutPath())
                ? "LaunchSettingsEditShortcut"
                : "LaunchSettingsCreateShortcut");
    }
    private void ButtonShortcut_OnClick(object? sender, RoutedEventArgs e)
    {
        object? shellObject = null;
        object? shortcutObject = null;

        try
        {
            string gamePath = Game.GetPath();
            string exePath = Path.Combine(gamePath, "BF2142.exe");
            string shortcutPath = GetShortcutPath();

            if (!File.Exists(exePath))
                throw new FileNotFoundException("BF2142.exe not found", exePath);

            bool existed = File.Exists(shortcutPath);

            string[] args = Game.BuildLaunchParams(GetCurrentParameters());

            string arguments = string.Join(" ",
                args.Select(QuoteArgument));

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");

            if (shellType == null)
                throw new InvalidOperationException("WScript.Shell unavailable");

            shellObject = Activator.CreateInstance(shellType)
                ?? throw new InvalidOperationException("Failed to create WScript.Shell");

            dynamic shell = shellObject;

            shortcutObject = shell.CreateShortcut(shortcutPath);
            dynamic shortcut = shortcutObject;

            shortcut.TargetPath = exePath;
            shortcut.WorkingDirectory = gamePath;
            shortcut.Arguments = arguments;
            shortcut.Description = "Battlefield 2142";
            shortcut.IconLocation = exePath;
            shortcut.Save();

            ButtonShortcut.Content = Locale.Get("LaunchSettingsShortcutSuccess");

            Console.WriteLine(
                $"[LaunchSettings] Shortcut {(existed ? "updated" : "created")}: {shortcutPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LaunchSettings] Shortcut error: {ex}");

            // Здесь можно будет подключить нормальное окно уведомлений,
            // если в лаунчере появится соответствующая система.
        }
        finally
        {
            if (shortcutObject != null && Marshal.IsComObject(shortcutObject))
                Marshal.FinalReleaseComObject(shortcutObject);

            if (shellObject != null && Marshal.IsComObject(shellObject))
                Marshal.FinalReleaseComObject(shellObject);
        }
    }
    private static string QuoteArgument(string argument)
    {
        if (string.IsNullOrEmpty(argument))
            return "\"\"";

        if (!argument.Any(char.IsWhiteSpace) && !argument.Contains('"'))
            return argument;

        StringBuilder result = new();
        result.Append('"');

        int backslashes = 0;

        foreach (char c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                result.Append('\\', backslashes * 2 + 1);
                result.Append('"');
                backslashes = 0;
                continue;
            }

            result.Append('\\', backslashes);
            result.Append(c);
            backslashes = 0;
        }

        result.Append('\\', backslashes * 2);
        result.Append('"');

        return result.ToString();
    }
    private Dictionary<string, object> GetCurrentParameters()
    {
        Dictionary<string, object> parameters = new();

        foreach (ILaunchParam param in LaunchParams)
        {
            if (!Controls.TryGetValue(param.Id, out Control? control))
                continue;

            if (param is LaunchParamBool && control is CheckBox checkBox)
            {
                parameters[param.Id] = checkBox.IsChecked ?? false;
            }
            else if (param is LaunchParamDict p && control is ComboBox comboBox)
            {
                parameters[param.Id] = p.Dictionary.Keys
                    .ElementAtOrDefault(comboBox.SelectedIndex) ?? "";
            }
            else if (param is LaunchOptionDict && control is StackPanel panel)
            {
                parameters[param.Id] = panel.Children
                    .OfType<CheckBox>()
                    .Where(cb => cb.IsChecked == true)
                    .Select(cb => cb.Content?.ToString() ?? "")
                    .ToArray();
            }
            else if (param is LaunchParamCustom && control is StackPanel customPanel)
            {
                parameters[param.Id] = customPanel.Children
                    .OfType<TextBox>()
                    .FirstOrDefault()?.Text ?? "";
            }
        }

        return parameters;
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
        Dictionary<string, object> parameters = GetCurrentParameters();

        try
        {
            Game.SetLaunchParameters(parameters);

            try
            {
                IntroVideoService.SetIntrosDisabled(
                    Game.GetPath(),
                    CheckBoxNoIntro.IsChecked ?? false);
            }
            catch (GamePathNotSetException) { }
            catch (GamePathNotFoundException) { }

            if (DataContext is IDialogContext ctx)
                ctx.Close();
        }
        catch (Exception exception)
        {
            Logger.Error("Failed to save launch settings", exception);
            Console.WriteLine(exception);
        }
    }
   
}
