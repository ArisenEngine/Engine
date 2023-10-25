using NebulaEditor.Models.Startup;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEditor.ViewModels.Startup
{
    public class ProjectListViewModel : ViewModelBase
    {

        public ObservableCollection<ProjectInfo> ProjectsList { get; set; } = new ObservableCollection<ProjectInfo>()
        {
            new ProjectInfo()
            {
                    ProjectName = "Test 1"
            },
            new ProjectInfo()
            {
                ProjectName = "Test 2"
            }
        };

    }
}
