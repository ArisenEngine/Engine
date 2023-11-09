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
        public SceneHierarchyViewModel() : base()
        {
            Nodes.AddRange(new ObservableCollection<TreeNode>()
            {
                new TreeNode("New Scene", @"/Assets/Icons/entity-icon.png",
                    new ObservableCollection<TreeNode>()
                    {
                        new TreeNode("Sub-AAA-0", @"/Assets/Icons/entity-icon.png",
                            new ObservableCollection<TreeNode>()
                            {
                                new TreeNode("Sub-AAA-0", @"/Assets/Icons/entity-icon.png"),
                                new TreeNode("Sub-AAA-1", @"/Assets/Icons/entity-icon.png"),
                                new TreeNode("Sub-AAA-2", @"/Assets/Icons/entity-icon.png"),
                                new TreeNode("Sub-AAA-3", @"/Assets/Icons/entity-icon.png"),
                                new TreeNode("Sub-AAA-4", @"/Assets/Icons/entity-icon.png")

                         }),
                        new TreeNode("Sub-AAA-1", @"/Assets/Icons/entity-icon.png"),
                        new TreeNode("Sub-AAA-2", @"/Assets/Icons/entity-icon.png"),
                        new TreeNode("Sub-AAA-3", @"/Assets/Icons/entity-icon.png"),
                        new TreeNode("Sub-AAA-4", @"/Assets/Icons/entity-icon.png")

                    }),

            });
        }
    }
}
