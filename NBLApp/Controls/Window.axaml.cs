using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using System;
namespace NBLApp.Controls;
public class Window : ContentControl
{
    public static readonly RoutedEvent<RoutedEventArgs> ClickHideEvent =
        RoutedEvent.Register<Window, RoutedEventArgs>(
            nameof(ClickHide),
            RoutingStrategies.Bubble);
    public event EventHandler<RoutedEventArgs>? ClickHide
    {
        add => AddHandler(ClickHideEvent, value);
        remove => RemoveHandler(ClickHideEvent, value);
    }
    public static readonly RoutedEvent<RoutedEventArgs> ClickMinMaxEvent =
        RoutedEvent.Register<Window, RoutedEventArgs>(
            nameof(ClickMinMax),
            RoutingStrategies.Bubble);
    public event EventHandler<RoutedEventArgs>? ClickMinMax
    {
        add => AddHandler(ClickMinMaxEvent, value);
        remove => RemoveHandler(ClickMinMaxEvent, value);
    }
    public static readonly RoutedEvent<RoutedEventArgs> ClickCloseEvent =
        RoutedEvent.Register<Window, RoutedEventArgs>(
            nameof(ClickClose),
            RoutingStrategies.Bubble);
    public event EventHandler<RoutedEventArgs>? ClickClose
    {
        add => AddHandler(ClickCloseEvent, value);
        remove => RemoveHandler(ClickCloseEvent, value);
    }
    public static readonly StyledProperty<Control> TitleContentProperty =
        AvaloniaProperty.Register<Window, Control>(nameof(TitleContent));
    public Control TitleContent
    {
        get => GetValue(TitleContentProperty);
        set => SetValue(TitleContentProperty, value);
    }
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        if (e.NameScope.Find<Button>("PART_ButtonWinHide") is { } button1) button1.Click += (_, _) => RaiseEvent(new RoutedEventArgs(ClickHideEvent));
        if (e.NameScope.Find<Button>("PART_ButtonWinMinMax") is { } button2) button2.Click += (_, _) => RaiseEvent(new RoutedEventArgs(ClickMinMaxEvent));
        if (e.NameScope.Find<Button>("PART_ButtonWinClose") is { } button3) button3.Click += (_, _) => RaiseEvent(new RoutedEventArgs(ClickCloseEvent));
    }
}