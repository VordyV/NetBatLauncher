using Avalonia.Controls;
using System;
using System.Collections.Generic;
namespace NBLApp.Controls;
public class ViewPresenter<T>
{
    public string CurrentView => this._view;
    public event Action<string> OnLoadView;
    public event Action<string> OnShowView;
    public event Action<string> OnInitView;
    private Dictionary<string, Func<T, ViewPresenter<T>, object?, UserControl>> _views;
    private string? _defaultView;
    private string _view;
    private Dictionary<string, UserControl> _viewInstances;
    private ContentControl _content;
    private T _arg;
    public ViewPresenter(T arg, Dictionary<string, Func<T, ViewPresenter<T>, object?, UserControl>>? views = null, string? defaultView = null)
    {
        this._arg = arg;
        this._content = new();
        this._viewInstances = new();
        this._defaultView = defaultView;
        if (views != null)
        {
            this._views = views;
            if (this._defaultView != null) this.LoadView(this._defaultView);
        }
        else this._views = new();
    }
    public void AddView(string name, Func<T, ViewPresenter<T>, object?, UserControl> content)
    {
        if (this._views.ContainsKey(name)) throw new Exception($"View '{name}' already added");
        this._views.Add(name, content);
    }
    // Creates a new view instance every time
    public void LoadView(string name, object? arg = null, bool reload = false)
    {
        if (!this._views.ContainsKey(name)) throw new Exception($"View '{name}' does not exist");
        if (this._view == name && !reload) return;
        this._view = name;
        if (this._viewInstances.ContainsKey(name)) this._viewInstances.Remove(name);
        this._viewInstances.Add(name, this._views[name].Invoke(this._arg, this, arg));
        this.OnInitView?.Invoke(name);
        this._content.Content = this._viewInstances[name];
        this.OnLoadView?.Invoke(name);
    }
    // Creates a view instance only once, if it hasn't been created yet
    public void ShowView(string name, object? arg = null)
    {
        if (!this._views.ContainsKey(name)) throw new Exception($"View '{name}' does not exist");
        if (this._view == name) return;
        this._view = name;
        if (!this._viewInstances.ContainsKey(name))
        {
            this._viewInstances.Add(name, this._views[name].Invoke(this._arg, this, arg));
            this.OnInitView?.Invoke(name);
        }
        this._content.Content = this._viewInstances[name];
        this.OnShowView?.Invoke(name);
    }
    public void CloseView(string name)
    {
        if (this._viewInstances.ContainsKey(name)) this._viewInstances.Remove(name);
        if (this._view == name && this._defaultView != null) this.LoadView(this._defaultView);
    }
    public ContentControl Content => this._content;
}