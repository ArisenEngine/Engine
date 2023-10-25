using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using MsBox.Avalonia.ViewModels.Commands;
using NebulaEditor.Models.Startup;
using NebulaEditor.Utility;
using ReactiveUI;
using Serialization;

namespace NebulaEditor.ViewModels.Startup
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

        public ICommand SelectionChangedCommand { get; }

        public MainWindowViewModel()
        {
            m_OpeningProjectViewModel = new OpeningProjectViewModel();
            m_CreateProjectViewModel = new CreateProjectViewModel();

           
            SelectionChangedCommand = ReactiveCommand.Create(OnTabControlSelectionChanged);
        }

        public async Task<int> LoadEditorConfigAsync(Splash splash)
        {
            try
            {
                EditorConfig.Instance = SerializationUtil.Deserialize<EditorConfig>(EditorConfig.EDITOR_CONFIG_PATH);

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
                splash.Hide();
                await MessageBoxUtility.ShowMessageBoxStandard("Exception", $"{ex.Message}");

                return -1;
            }
            
        }

        
        private void OnTabControlSelectionChanged()
        {

        }

    }
}