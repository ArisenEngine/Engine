using System;
using System.Collections.Generic;
using ArisenEditor.ViewModels;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.ReactiveUI.Controls;

namespace ArisenEditor.Core.Factory;

internal class DockFactory : Dock.Model.ReactiveUI.Factory
{
    
    internal IRootDock CreateBaseLayout()
    {
        return new RootDock();
    }

    public override IDockWindow? CreateWindowFrom(IDockable dockable)
    {
        var window = base.CreateWindowFrom(dockable);

        if (window != null)
        {
            window.Title = dockable.Title;
        }
        
        return window;
    }
    
    public override IRootDock CreateLayout()
    {
        var sceneViewModel = new SceneViewModel() { Id = "Scene", Title = "Scene" };
        var gameView = new GameViewModel() { Id = "Game", Title = "Game" };
        var hierarchyView = new HierarchyViewModel() { Id = "Hierarchy", Title = "Hierarchy" };
        var inspectorView = new InspectorViewModel() { Id = "Inspector", Title = "Inspector" };
        var contentView = new ContentViewModel() { Id = "Content", Title = "Content" };
        var consoleView = new ConsoleViewModel() { Id = "Console", Title = "Console" };

        var documentDock = new RenderViewDocumentDock()
        {
            Proportion = 0.75,
            Id = "Document",
            Title = "Document",
            ActiveDockable = sceneViewModel,
            VisibleDockables = CreateList<IDockable>(sceneViewModel, gameView),
            CanCreateDocument = true,
            IsCollapsable = false,
        };

        var leftToolDock = new ToolDock()
        {
            Proportion = 0.25,
            Id = "LeftTool",
            Title = "LeftTool",
            ActiveDockable = hierarchyView,
            Alignment = Alignment.Left,
            VisibleDockables = CreateList<IDockable>(hierarchyView)
        };

        var rightToolDock = new ToolDock()
        {
            Id = "RightTool",
            Title = "RightTool",
            ActiveDockable = inspectorView,
            Alignment = Alignment.Right,
            VisibleDockables = CreateList<IDockable>(inspectorView)
        };

        var bottomToolDock = new ProportionalDock()
        {
            Proportion = 0.25,
            Id = "BottomTool",
            Title = "BottomTool",
            ActiveDockable = contentView,
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                new ToolDock
                {
                    ActiveDockable = contentView,
                    VisibleDockables = CreateList<IDockable>(contentView),
                    Alignment = Alignment.Left
                },
                new ProportionalDockSplitter(),
                new ToolDock
                {
                    ActiveDockable = consoleView,
                    VisibleDockables = CreateList<IDockable>(consoleView),
                    Alignment = Alignment.Right
                }
                ),
        };
        
        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Title = "MainLayout",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
               new ProportionalDock()
               {
                   Proportion = 0.8,
                   Id = "LeftPart",
                   Orientation = Orientation.Vertical,
                   VisibleDockables = CreateList<IDockable>(
                      new ProportionalDock()
                      {
                          Proportion = 0.75,
                          Id = "DocumentAndHierarchy",
                          Orientation = Orientation.Horizontal,
                          VisibleDockables = CreateList<IDockable>( 
                              leftToolDock,
                              new ProportionalDockSplitter(),
                              documentDock),
                      },
                       new ProportionalDockSplitter(),
                       bottomToolDock
                       ),
               },
               new ProportionalDockSplitter(),
               new ProportionalDock()
               {
                   Proportion = 0.2,
                   Id = "RightPart",
                   Orientation = Orientation.Vertical,
                   VisibleDockables = CreateList<IDockable>(rightToolDock),
               }
            )
        };
        
        
        // 创建根布局
        var rootDock = CreateRootDock();
        rootDock.Id = "Root";
        rootDock.Title = "Root";
        rootDock.ActiveDockable = mainLayout;
        rootDock.DefaultDockable = mainLayout;
        rootDock.IsCollapsable = false;
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        
        
        return rootDock;
    }
    
    public override void InitLayout(IDockable layout)
    {
        HostWindowLocator = new Dictionary<string, Func<IHostWindow?>>
        {
            [nameof(IDockWindow)] = () => new HostWindow()
        };
        base.InitLayout(layout);
    }
}