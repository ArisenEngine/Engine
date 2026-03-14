using System;
using ArisenEditorFramework.Core;
using ReactiveUI;

namespace ArisenEditor.Core.Views;

internal class FooterViewModel : EditorPanelBase
{
    public override string Title => "Footer";
    public override string Id => "Footer";
    public override object Content => new FooterView { DataContext = this };

    private string _statusText = "Engine Ready";
    public string StatusText
    {
        get => _statusText;
        set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public FooterViewModel()
    {
    }
}
