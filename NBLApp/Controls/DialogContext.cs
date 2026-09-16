using CommunityToolkit.Mvvm.ComponentModel;
using Irihi.Avalonia.Shared.Contracts;
using System;
namespace NBLApp.Controls;
public partial class DialogContext : ObservableObject, IDialogContext
{
    public void Close()
    {
        RequestClose?.Invoke(this, true);
    }
    public event EventHandler<object?>? RequestClose;
}