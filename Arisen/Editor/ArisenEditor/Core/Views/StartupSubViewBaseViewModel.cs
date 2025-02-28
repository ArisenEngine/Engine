using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ArisenEditor.Models.Startup;
using ArisenEditor.Utilities;
using Avalonia.Media.Imaging;
using ReactiveUI;

namespace ArisenEditor.ViewModels.Startup
{
    public abstract class StartupSubViewBaseViewModel : ReactiveObject
    {
        protected Bitmap m_PreviewImage;
        internal Bitmap PreivewImage
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
        internal void UpdatePreviewImage(string path)
        {
            if (string.IsNullOrEmpty(path) || path == m_PreviewPath)
            {
                return;
            }

            m_PreviewPath = path;
            PreivewImage = ImageHelper.LoadFromResource(path);
        }
        
        internal ProjectListViewModel ProjectListViewModel { get; set; } = new ProjectListViewModel();

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
        
        internal void SetProjectsList(List<ProjectInfo> projectInfos)
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