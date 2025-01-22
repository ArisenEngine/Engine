using System.Windows.Input;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using Serialization;
using System.Diagnostics;
using ArisenEditor.GameDev;
using ArisenEditor.Models.Startup;
using ArisenEditor.Utilities;

namespace ArisenEditor.ViewModels.Startup
{
    public class CreateProjectViewModel : StartupSubViewBaseViewModel
    {
        #region Project Info

        private string m_NewProjectName = "New Project";
        public string NewProjectName
        {
            get
            {
                return m_NewProjectName;
            }

            set => this.RaiseAndSetIfChanged(ref m_NewProjectName, value);

        }

        private string m_NewProjectDesc = "a arisen project";
        public string NewProjectDesc
        {
            get
            {
                return m_NewProjectDesc;
            }

            set => this.RaiseAndSetIfChanged(ref m_NewProjectDesc, value);
        }

        private string m_NewProjectPath = ProjectInfo.DefaultProjectPath;
        public string NewProjectPath
        {
            get
            {
                return m_NewProjectPath;
            }

            set => this.RaiseAndSetIfChanged(ref m_NewProjectPath, value);

        }
        #endregion

        public ICommand? BroweProjectLocationCommand { get; }
        public ICommand? CreateProjectCommand { get; }

        public CreateProjectViewModel(): base()
        {
            BroweProjectLocationCommand = ReactiveCommand.Create(async () =>
            {
                var pathList = await FileSystemUtilities.BrowseDictionary("Select your project location");

                if (pathList != null && pathList.Count > 0)
                {
                    NewProjectPath = pathList[0];
                }
                else
                {
                    _ = MessageBoxUtility.ShowMessageBoxStandard("Error", "Please select a location");
                }
            });

            CreateProjectCommand = ReactiveCommand.Create(this.CreateProject);
        }

        /// <summary>
        /// path to project inside
        /// </summary>
        private string m_ProjectPath;
        private string m_ErrorMsg = "Unknow error.";

        private bool ProjectPathValidate()
        {
            if (string.IsNullOrEmpty(NewProjectPath)) { return false; }

            m_ProjectPath = NewProjectPath.Replace('/', Path.DirectorySeparatorChar);

            if (!Path.EndsInDirectorySeparator(m_ProjectPath))
            {
                m_ProjectPath += Path.DirectorySeparatorChar;
            }

            m_ProjectPath += @$"{NewProjectName}{Path.DirectorySeparatorChar}";

            if (Directory.Exists(m_ProjectPath) && Directory.EnumerateFileSystemEntries(m_ProjectPath).Any())
            {
                m_ErrorMsg = "Project folder is not empty.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(m_ProjectPath.Trim()))
            {
                m_ErrorMsg = "Please type in a project name.";
                return false;
            }

            if (NewProjectName.IndexOfAny(Path.GetInvalidFileNameChars()) != -1)
            {
                m_ErrorMsg = "Project name contains invalid characters.";
                return false;
            }

           

            if (NewProjectPath.IndexOfAny(Path.GetInvalidPathChars()) != -1)
            {
                m_ErrorMsg = "Project path contains invalid characters:";
                var invalidChars = Path.GetInvalidPathChars();
                foreach (char c in invalidChars)
                {
                    if (NewProjectPath.IndexOf(c) != -1)
                    {
                        m_ErrorMsg += " ' " + c + " ' ";
                    }
                }
                m_ErrorMsg += " .";
                return false;
            }

            return true;
        }

        private void CreateProject()
        {
            if (ProjectPathValidate())
            {
                try 
                {
                    
                    DoCreateProject();

                } catch (Exception e)
                {
                    _ = MessageBoxUtility.ShowMessageBoxStandard("Project creation error", e.Message);
                }
            } 
            else
            {
                _ = MessageBoxUtility.ShowMessageBoxStandard("Error", m_ErrorMsg);
            }
        }
        

        private void DoCreateProject()
        {
            // copy from template and handle sln file
            if (ProjectListViewModel.SelectedIndex < 0)
            {
                _ = MessageBoxUtility.ShowMessageBoxStandard("Project creation error", "Please select a template first!");
                
                return;
            }


            var selectedTemplate = this.ProjectListViewModel.ProjectsList[this.ProjectListViewModel.SelectedIndex];

            Debug.Assert(selectedTemplate != null);

            ProjectSolution.CreateProjectSolution(selectedTemplate, NewProjectPath, NewProjectName);

            // TODO: complete the icon, previewImage infos 
            var currentProject = new ProjectInfo()
            {
                ProjectName = NewProjectName,
                ProjectPath = NewProjectPath,
                IconURL = selectedTemplate.IconURL,
                PreviewImageURL = selectedTemplate.PreviewImageURL,
                Desc = NewProjectDesc,
            };

            // save to project list
            bool needUpdate = true;
            for(int i = 0;i < EditorConfig.Instance.Projects.Count; ++i)
            {
                if (
                    Path.Combine(EditorConfig.Instance.Projects[i].ProjectPath, EditorConfig.Instance.Projects[i].ProjectName)
                    == Path.Combine(currentProject.ProjectPath, currentProject.ProjectName))
                {
                    needUpdate = false;
                    break;
                }
            }

            if (needUpdate)
            {
                EditorConfig.Instance.Projects.Add(currentProject);
                SerializationUtil.Serialize<EditorConfig>(EditorConfig.Instance, EditorConfig.EDITOR_CONFIG_PATH);
            }
            

            OpeningProjectViewModel.OpenProject(currentProject);
        }

    }
}