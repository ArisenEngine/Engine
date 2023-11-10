using NebulaEditor.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DynamicData;

namespace NebulaEditor.ViewModels
{
    public class AssetsHierarchyViewModel : HierarchyViewModel
    {
        public static readonly string FolderIconPath = @"/Assets/Icons/folder.png";
        public static readonly string FolderOpenIconPath = @"/Assets/Icons/open-folder.png";
        public AssetsHierarchyViewModel() : base()
        {
            Nodes.AddRange(new ObservableCollection<TreeNode>()
            {
                new TreeNode("Assets", FolderIconPath,
                    new ObservableCollection<TreeNode>()
                    {
                        new TreeNode("Sub-AAA-0", FolderIconPath,
                            new ObservableCollection<TreeNode>()
                            {
                                new TreeNode("Sub-AAA-0", FolderIconPath),
                                new TreeNode("Sub-AAA-1", FolderIconPath),
                                new TreeNode("Sub-AAA-2", FolderIconPath),
                                new TreeNode("Sub-AAA-3", FolderIconPath),
                                new TreeNode("Sub-AAA-4", FolderIconPath)

                            }),
                        new TreeNode("Sub-AAA-1", FolderIconPath),
                        new TreeNode("Sub-AAA-2", FolderIconPath),
                        new TreeNode("Sub-AAA-3", FolderIconPath),
                        new TreeNode("Sub-AAA-4", FolderIconPath)

                    }),

            });
        }
    }
}
