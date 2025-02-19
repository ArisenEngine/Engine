using System;
using System.Collections.Generic;
using ArisenEditor.ViewModels;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

namespace ArisenEditor.Core.Factory;

internal class DockFactory : Dock.Model.Mvvm.Factory
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

        var documentDock = new DocumentDock()
        {
            Id = "Document",
            Title = "Document",
            ActiveDockable = sceneViewModel,
            VisibleDockables = CreateList<IDockable>(sceneViewModel, gameView),
            CanCreateDocument = true,
            IsCollapsable = false,
        };

        var leftToolDock = new ToolDock()
        {
            Id = "LeftTool",
            Title = "LeftTool",
            ActiveDockable = hierarchyView,
            VisibleDockables = CreateList<IDockable>(hierarchyView)
        };

        var rightToolDock = new ToolDock()
        {
            Id = "RightTool",
            Title = "RightTool",
            ActiveDockable = inspectorView,
            VisibleDockables = CreateList<IDockable>(inspectorView)
        };

        var bottomToolDock = new ToolDock()
        {
            Id = "BottomTool",
            Title = "BottomTool",
            ActiveDockable = contentView,
            VisibleDockables = CreateList<IDockable>(contentView),
        };
        
        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Title = "MainLayout",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
               new ProportionalDock()
               {
                   Id = "LeftPart",
                   Orientation = Orientation.Vertical,
                   VisibleDockables = CreateList<IDockable>(
                      new ProportionalDock()
                      {
                          Id = "DocumentAndHierarchy",
                          Orientation = Orientation.Horizontal,
                          VisibleDockables = CreateList<IDockable>( 
                              leftToolDock,
                              new ProportionalDockSplitter(),
                              documentDock),
                      },
                       new ProportionalDockSplitter(),
                   new ProportionalDock()
                   {
                       Id = "ContentAndConsole",
                       Orientation = Orientation.Horizontal,
                       VisibleDockables = CreateList<IDockable>(bottomToolDock),
                   }
                       ),
                   
                   
               },
               new ProportionalDockSplitter(),
               new ProportionalDock()
               {
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