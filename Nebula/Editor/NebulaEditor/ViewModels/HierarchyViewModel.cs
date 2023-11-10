using Core.ECS.Entity;
using DynamicData;
using NebulaEditor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using ReactiveUI;

namespace NebulaEditor.ViewModels
{
    public class HierarchyViewModel : ViewModelBase
    {
        public ObservableCollection<TreeNode> Nodes { get; } = new ObservableCollection<TreeNode>();
        public ObservableCollection<TreeNode> SelectedNodes { get; } = new ObservableCollection<TreeNode>();

        public HierarchyViewModel()
        {
            
        }

    }
}
