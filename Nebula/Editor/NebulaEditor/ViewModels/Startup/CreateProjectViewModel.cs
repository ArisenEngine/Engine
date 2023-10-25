

using NebulaEditor.Models.Startup;
using System.Windows.Input;
using ReactiveUI;
using System;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using NebulaEditor.Utility;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using NebulaEditor.Windows.MainEditor;
using System.Linq;

namespace NebulaEditor.ViewModels.Startup
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

        private string m_NewProjectDesc = "a nebula project";
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

        public CreateProjectViewModel()
        {
            BroweProjectLocationCommand = ReactiveCommand.Create(async () =>
            {
                var pathList = await FileSystemUtility.BrowseDictionary("Select your project location");

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
                    Directory.CreateDirectory(m_ProjectPath);

                    for(int i = 0; i < ProjectInfo.Folders.Count; ++i)
                    {
                        var folderName = ProjectInfo.Folders[i];
                        var folderPath = m_ProjectPath + Path.DirectorySeparatorChar + folderName;
                        var folderInfo = Directory.CreateDirectory(folderPath);

                        if (folderInfo != null && folderName.StartsWith('.'))
                        {
                            folderInfo.Attributes |= FileAttributes.Hidden;
                        }
                    }


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
        

    }
}