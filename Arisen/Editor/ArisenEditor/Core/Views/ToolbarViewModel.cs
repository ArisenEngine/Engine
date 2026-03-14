using System;
using System.Reactive;
using ArisenEditorFramework.Core;
using ReactiveUI;

namespace ArisenEditor.Core.Views;

internal class ToolbarViewModel : EditorPanelBase
{
    public override string Title => "Toolbar";
    public override string Id => "Toolbar";
    public override object Content => new ToolBarView { DataContext = this };

    public ReactiveCommand<Unit, Unit> PlayCommand { get; }
    public ReactiveCommand<Unit, Unit> PauseCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> BuildCommand { get; }
    
    public ReactiveCommand<Unit, Unit> SelectCommand { get; }
    public ReactiveCommand<Unit, Unit> MoveCommand { get; }
    public ReactiveCommand<Unit, Unit> RotateCommand { get; }
    public ReactiveCommand<Unit, Unit> ScaleCommand { get; }
    public ReactiveCommand<Unit, Unit> NavCommand { get; }
    public ReactiveCommand<Unit, Unit> PhysicsCommand { get; }

    public ToolbarViewModel()
    {
        PlayCommand = ReactiveCommand.Create(() => Console.WriteLine("Play..."));
        PauseCommand = ReactiveCommand.Create(() => Console.WriteLine("Pause..."));
        StopCommand = ReactiveCommand.Create(() => Console.WriteLine("Stop..."));
        BuildCommand = ReactiveCommand.Create(() => Console.WriteLine("Building Project..."));
        
        SelectCommand = ReactiveCommand.Create(() => Console.WriteLine("Select Mode"));
        MoveCommand = ReactiveCommand.Create(() => Console.WriteLine("Move Mode"));
        RotateCommand = ReactiveCommand.Create(() => Console.WriteLine("Rotate Mode"));
        ScaleCommand = ReactiveCommand.Create(() => Console.WriteLine("Scale Mode"));
        NavCommand = ReactiveCommand.Create(() => Console.WriteLine("Navigation Mode"));
        PhysicsCommand = ReactiveCommand.Create(() => Console.WriteLine("Physics Mode"));
    }
}
