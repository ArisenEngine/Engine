//using Core.ECS.Entity;
using DynamicData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NebulaEditor.Models;

namespace NebulaEditor.ViewModels
{
    public class SceneHierarchyViewModel : HierarchyViewModel
    {
        public static readonly string SceneIconPath = @"/Assets/Icons/clapperboard.png";
        public static readonly string EntityIconPath = @"/Assets/Icons/entity-icon.png";
        
        public SceneHierarchyViewModel() : base()
        {
            Nodes.AddRange(new ObservableCollection<TreeNode>()
            {
                new TreeNode("New Scene", SceneIconPath,
                    new ObservableCollection<TreeNode>()
                    {
                        new TreeNode("Sub-AAA-0", EntityIconPath,
                            new ObservableCollection<TreeNode>()
                            {
                                new TreeNode("Sub-AAA-0", EntityIconPath),
                                new TreeNode("Sub-AAA-1", EntityIconPath),
                                new TreeNode("Sub-AAA-2", EntityIconPath),
                                new TreeNode("Sub-AAA-3", EntityIconPath),
                                new TreeNode("Sub-AAA-4", EntityIconPath)
            
                         }),
                        new TreeNode("Sub-AAA-1", EntityIconPath),
                        new TreeNode("Sub-AAA-2", EntityIconPath),
                        new TreeNode("Sub-AAA-3", EntityIconPath),
                        new TreeNode("Sub-AAA-4", EntityIconPath)
            
                    }),
            
            });
        }
    }
}
