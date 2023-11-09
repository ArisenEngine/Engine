using NebulaEditor.Models.Startup;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NebulaEditor.ViewModels.Startup
{
	public abstract class StartupSubViewBaseViewModel : NebulaEditor.ViewModels.ViewModelBase
    {
        public ProjectListViewModel ProjectListViewModel { get; set; } = new ProjectListViewModel();

        public void SetProjectsList(List<ProjectInfo> projectInfos)
        {
            var observerProjectList = new ObservableCollection<ProjectInfo>();
            for(var i = 0;i < projectInfos.Count; ++i) 
            {
                observerProjectList.Add(projectInfos[i]);
            }
            ProjectListViewModel.ProjectsList = observerProjectList;
        }

    }
}