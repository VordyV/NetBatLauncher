using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Irihi.Avalonia.Shared.Contracts;
using NBL;

namespace NBLApp.Forms;

public partial class Disclaimer : UserControl
{
    protected Game Game;
    protected Launcher Launcher;
    
    public Disclaimer()
    {
        InitializeComponent();
    }
    
    public Disclaimer(Launcher launcher, Game game)
    {
        Game = game;
        Launcher = launcher;
        InitializeComponent();
        Loaded += async (sender, args) => await OnLoaded(sender, args);
    }

    private async Task OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            string? text = await this.Launcher.Server.GetGameDisclaimer(this.Game.Id);
            this.MarkdownLineText.Markdown = text ?? "";
        }
        catch (Exception exception)
        {
            this.MarkdownLineText.Markdown = "Failed to retrieve the text :(";
        }
    }

    private void Button_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IDialogContext ctx) ctx.Close();
    }
}