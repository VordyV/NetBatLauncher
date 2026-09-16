using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
namespace NBLApp.Controls;
public class Form : ContentControl
{
    public static readonly StyledProperty<Control> ActionContentProperty =
        AvaloniaProperty.Register<Form, Control>(nameof(ActionContent));
    public Control ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<Form, string>(nameof(Title), "");
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
    public static readonly StyledProperty<string> SubTitleProperty =
        AvaloniaProperty.Register<Form, string>(nameof(SubTitle), "");
    public string SubTitle
    {
        get => GetValue(SubTitleProperty);
        set => SetValue(SubTitleProperty, value);
    }
}