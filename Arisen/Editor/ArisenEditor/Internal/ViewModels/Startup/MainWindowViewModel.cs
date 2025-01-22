using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using ArisenEditor.GameDev;
using ArisenEditor.Models.Startup;
using ArisenEditor.Utilities;
using Avalonia.Controls;
using MsBox.Avalonia.ViewModels.Commands;
using ReactiveUI;
using Serialization;

namespace ArisenEditor.ViewModels.Startup
{
    public class MainWindowViewModel : ViewModelBase
    {
        

        private OpeningProjectViewModel m_OpeningProjectViewModel;
        public OpeningProjectViewModel OpeningProjectViewModel
        {
            get
            {
                return m_OpeningProjectViewModel;
            }
        }

        private CreateProjectViewModel m_CreateProjectViewModel;
        public CreateProjectViewModel CreateProjectViewModel
        {
            get
            {
                return m_CreateProjectViewModel;
            }
        }

        internal ICommand SelectionChangedCommand { get; }

        public MainWindowViewModel()
        {
            m_OpeningProjectViewModel = new OpeningProjectViewModel();
            m_CreateProjectViewModel = new CreateProjectViewModel();

           
            SelectionChangedCommand = ReactiveCommand.Create(OnTabControlSelectionChanged);
        }

        private void ProjectsListValidation()
        {
            var toRemove = new List<ProjectInfo>();
            for(int i = 0; i < EditorConfig.Instance.Projects.Count; ++i)
            {
                var project = EditorConfig.Instance.Projects[i];
                if (!ProjectSolution.ProjectValidation(project))
                {
                    // invalid project
                    toRemove.Add(project);
                }
            }

            var dirty = false;
            while(toRemove.Count > 0)
            {
                dirty = true;
                EditorConfig.Instance.Projects.Remove(toRemove[0]);
                toRemove.RemoveAt(0);
            }

            if (dirty)
            {
                SerializationUtil.Serialize<EditorConfig>(EditorConfig.Instance, EditorConfig.EDITOR_CONFIG_PATH);
            }
        }

        internal async Task<int> LoadEditorConfigAsync()
        {
            if (EditorConfig.Instance != null)
            {
                OpeningProjectViewModel.SetProjectsList(EditorConfig.Instance.Projects);
                var templates = SerializationUtil.Deserialize<Templates>(EditorConfig.Instance.TemplatesPath);

                if (templates.IsDirty)
                {
                    SerializationUtil.Serialize(templates, EditorConfig.Instance.TemplatesPath);
                }

                CreateProjectViewModel.SetProjectsList(templates.Projects);
                return 0;
            }
            
            try
            {
                EditorConfig.Instance = SerializationUtil.Deserialize<EditorConfig>(EditorConfig.EDITOR_CONFIG_PATH);

                ProjectsListValidation();

                OpeningProjectViewModel.SetProjectsList(EditorConfig.Instance.Projects);

                var templates = SerializationUtil.Deserialize<Templates>(EditorConfig.Instance.TemplatesPath);

                if (templates.IsDirty)
                {
                    SerializationUtil.Serialize(templates, EditorConfig.Instance.TemplatesPath);
                }

                CreateProjectViewModel.SetProjectsList(templates.Projects);

                return 0;
            }
            catch(Exception ex)
            {
                
                await MessageBoxUtility.ShowMessageBoxStandard("Exception", $"{ex.Message}");

                return -1;
            }
            
        }

        
        private void OnTabControlSelectionChanged()
        {

        }

    }
}