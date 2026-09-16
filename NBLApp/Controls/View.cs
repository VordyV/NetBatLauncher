using Avalonia.Controls;
using NBL;
namespace NBLApp.Controls;
public class View : UserControl
{
    protected ViewPresenter<Launcher> ViewPresenter;
    protected Launcher Launcher;
    protected object? Arg;
    public View() { }
    public View(Launcher launcher, ViewPresenter<Launcher> viewPresenter, object? arg)
    {
        this.Launcher = launcher;
        this.ViewPresenter = viewPresenter;
        this.Arg = arg;
    }
}