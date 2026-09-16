using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using NBL;
using NBL.Services;
using Irihi.Avalonia.Shared.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using static NBL.LaunchParamDict;
namespace NBLApp.Forms;
public partial class LaunchSettings : UserControl
{
    protected Game Game;
    protected ILaunchParam[] LaunchParams;
    protected Dictionary<string, Control> Controls =
        new();
    private IntroVideoService IntroVideoService;
    public LaunchSettings()
    {
        InitializeComponent();
        this.DataContext =
            this;
    }
    public LaunchSettings(Game game)
    {
        this.Game =
            game;
        this.LaunchParams =
            game.LaunchParams.Values.ToArray();
        this.IntroVideoService =
            new IntroVideoService();
        InitializeComponent();
        this.DataContext =
            this;
        StackPanel parametersPanel =
            new StackPanel
            {
                Spacing = 12
            };
        this.MainContent.Content =
            parametersPanel;
        try
        {
            string gamePath =
                this.Game.GetPath();
            this.CheckBoxNoIntro.IsChecked =
                this.IntroVideoService.AreIntrosDisabled(
                    gamePath);
            this.TextBlockNoIntroStatus.Text =
                "Видео вступления будут отключены";
        }
        catch (GamePathNotSetException)
        {
            this.CheckBoxNoIntro.IsChecked =
                false;
            this.CheckBoxNoIntro.IsEnabled =
                false;
            this.TextBlockNoIntroStatus.Text =
                "Игра ещё не установлена";
        }
        catch (GamePathNotFoundException)
        {
            this.CheckBoxNoIntro.IsChecked =
                false;
            this.CheckBoxNoIntro.IsEnabled =
                false;
            this.TextBlockNoIntroStatus.Text =
                "Игра ещё не установлена";
        }
        Dictionary<string, object> savedParameters =
            this.Game.GetLaunchParameters();
        foreach (ILaunchParam param in this.LaunchParams)
        {
            Control control;
            if (param is LaunchParamBool)
            {
                bool value =
                    savedParameters.TryGetValue(
                        param.Id,
                        out object? savedValue)
                    && savedValue is bool boolValue
                        ? boolValue
                        : false;
                control =
                    new CheckBox
                    {
                        Content =
                            param.Name,
                        IsChecked =
                            value
                    };
                this.Controls.Add(
                    param.Id,
                    control);
            }
            else if (param is LaunchParamDict p)
            {
                List<ComboBoxItem> items =
                    new();
                foreach (string key in p.Dictionary.Keys)
                {
                    items.Add(
                        new ComboBoxItem
                        {
                            Content =
                                key
                        });
                }
                string selectedValue =
                    savedParameters.TryGetValue(
                        param.Id,
                        out object? savedValue)
                    && savedValue is string stringValue
                        ? stringValue
                        : p.Dictionary.Keys.FirstOrDefault()
                            ?? "";
                ComboBox combo =
                    new ComboBox
                    {
                        ItemsSource =
                            items,
                        SelectedIndex =
                            Math.Max(
                                0,
                                p.Dictionary.Keys
                                    .ToList()
                                    .IndexOf(
                                        selectedValue)),
                        HorizontalAlignment =
                            HorizontalAlignment.Stretch
                    };
                control =
                    combo;
                this.Controls.Add(
                    param.Id,
                    control);
            }
            else if (param is LaunchOptionDict options)
            {
                StackPanel optionsPanel =
                    new StackPanel
                    {
                        Spacing = 6
                    };
                string[] selectedValues =
                    savedParameters.TryGetValue(
                        param.Id,
                        out object? savedValue)
                    && savedValue is string[] values
                        ? values
                        : Array.Empty<string>();
                foreach (string key in options.Dictionary.Keys)
                {
                    CheckBox checkBox =
                        new CheckBox
                        {
                            Content =
                                key,
                            IsChecked =
                                selectedValues.Contains(
                                    key)
                        };
                    optionsPanel.Children.Add(
                        checkBox);
                }
                control =
                    optionsPanel;
                this.Controls.Add(
                    param.Id,
                    control);
            }
            else if (param is LaunchParamCustom)
            {
                string value =
                    savedParameters.TryGetValue(
                        param.Id,
                        out object? savedValue)
                    && savedValue is string stringValue
                        ? stringValue
                        : "";
                StackPanel panel =
                    new StackPanel
                    {
                        Spacing = 6
                    };
                TextBox textBox =
                    new TextBox
                    {
                        Watermark =
                            "Ввод свойств вручную (например +nosound 1)",
                        Text =
                            value,
                        HorizontalAlignment =
                            HorizontalAlignment.Stretch
                    };
                panel.Children.Add(
                    textBox);
                control =
                    panel;
                this.Controls.Add(
                    param.Id,
                    control);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Launch parameter '{param.Name}' type not specified");
            }
            StackPanel parameterPanel =
                new StackPanel
                {
                    Spacing = 5
                };
            if (control is not CheckBox)
            parameterPanel.Children.Add(
                control);
            parametersPanel.Children.Add(
                parameterPanel);
        }
    }
    private void ButtonCancel_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        if (this.DataContext
            is IDialogContext ctx)
        {
            ctx.Close();
        }
    }
    private void ButtonSave_OnClick(
        object? sender,
        RoutedEventArgs e)
    {
        Dictionary<string, object> parameters =
            new();
        foreach (ILaunchParam launchParam in this.LaunchParams)
        {
            if (!this.Controls.TryGetValue(
                    launchParam.Id,
                    out Control? control))
            {
                continue;
            }
            if (launchParam is LaunchParamBool)
            {
                parameters[launchParam.Id] =
                    ((CheckBox)control).IsChecked
                    ?? false;
            }
            else if (launchParam is LaunchParamDict)
            {
                ComboBox combo =
                    (ComboBox)control;
                if (combo.SelectedItem
                    is ComboBoxItem item)
                {
                    parameters[launchParam.Id] =
                        item.Content?.ToString()
                        ?? "";
                }
            }
            else if (launchParam is LaunchOptionDict)
            {
                StackPanel optionsPanel =
                    (StackPanel)control;
                List<string> selectedOptions =
                    new();
                foreach (Control child
                    in optionsPanel.Children)
                {
                    if (child is CheckBox checkBox
                        && checkBox.IsChecked == true
                        && checkBox.Content is string option)
                    {
                        selectedOptions.Add(
                            option);
                    }
                }
                parameters[launchParam.Id] =
                    selectedOptions.ToArray();
            }
            else if (launchParam is LaunchParamCustom)
            {
                StackPanel panel =
                    (StackPanel)control;
                TextBox? textBox =
                    panel.Children
                        .OfType<TextBox>()
                        .FirstOrDefault();
                parameters[launchParam.Id] =
                    textBox?.Text
                    ?? "";
            }
        }
        try
        {
            this.Game.SetLaunchParameters(
                parameters);
            try
            {
                string gamePath =
                    this.Game.GetPath();
                this.IntroVideoService.SetIntrosDisabled(
                    gamePath,
                    this.CheckBoxNoIntro.IsChecked
                    ?? false);
            }
            catch (GamePathNotSetException)
            {
                // Игра ещё не установлена.
            }
            catch (GamePathNotFoundException)
            {
                // Путь игры сохранён,
                // но сама игра отсутствует.
            }
            if (this.DataContext
                is IDialogContext ctx)
            {
                ctx.Close();
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine(
                exception);
        }
    }
}