using NebulaEditor.Models.Startup;
using System.Collections.Generic;

namespace NebulaEditor.ViewModels.Startup
{
	public abstract class StartupSubViewBaseViewModel : ViewModelBase
	{
        public ProjectListViewModel ProjectListViewModel { get; set; } = new ProjectListViewModel();

        public void SetProjectsList(List<ProjectInfo> projectInfos)
        {
            ProjectListViewModel.ProjectsList = projectInfos;
        }

    }
}