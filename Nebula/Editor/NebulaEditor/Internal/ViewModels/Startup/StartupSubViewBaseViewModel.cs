using System;
using NebulaEditor.Models.Startup;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using NebulaEditor.Utilities;
using ReactiveUI;

namespace NebulaEditor.ViewModels.Startup
{
	public abstract class StartupSubViewBaseViewModel : NebulaEditor.ViewModels.ViewModelBase
    {
        protected Bitmap m_PreviewImage;
        public Bitmap PreivewImage
        {
            get
            {
                return m_PreviewImage;
            }
            set
            {
                this.RaiseAndSetIfChanged(ref m_PreviewImage, value);
            }
        }

        private string m_PreviewPath = String.Empty;
        public void UpdatePreviewImage(string path)
        {
            if (string.IsNullOrEmpty(path) || path == m_PreviewPath)
            {
                return;
            }

            m_PreviewPath = path;
            PreivewImage = ImageHelper.LoadFromResource(path);
        }
        
        public ProjectListViewModel ProjectListViewModel { get; set; } = new ProjectListViewModel();

        public StartupSubViewBaseViewModel() : base()
        {
            PreivewImage = ImageHelper.LoadFromResource("/Assets/LOGO.png");
            ProjectListViewModel.OnSelectionChanged += (int selectedIndex) =>
            {

                if (selectedIndex >= 0 &&
                    selectedIndex < ProjectListViewModel.ProjectsList.Count)
                {
                    var current = ProjectListViewModel.ProjectsList[selectedIndex];
                    UpdatePreviewImage(current.PreviewImageURL);
                }
            };
        }
        
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