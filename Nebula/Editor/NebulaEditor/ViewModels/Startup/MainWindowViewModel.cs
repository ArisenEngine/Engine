using System;
using System.Threading.Tasks;
using NebulaEditor.Models.Startup;
using NebulaEditor.Utility;
using Serialization;

namespace NebulaEditor.ViewModels.Startup
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly static string EDITOR_CONFIG_PATH = "configs/editor_config.yaml";

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

        public MainWindowViewModel()
        {
            m_OpeningProjectViewModel = new OpeningProjectViewModel();
            m_CreateProjectViewModel = new CreateProjectViewModel();
        }

        public async Task<int> LoadEditorConfigAsync(Splash splash)
        {
            try
            {
                EditorConfig.Instance = SerializationUtil.Deserialize<EditorConfig>(EDITOR_CONFIG_PATH);

                OpeningProjectViewModel.SetProjectsList(EditorConfig.Instance.Projects);

                var templates = SerializationUtil.Deserialize<Templates>(EditorConfig.Instance.TemplatesPath);
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

        
    }
}