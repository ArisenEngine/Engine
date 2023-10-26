
using Serialization.Interface;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace NebulaEditor.Models.Startup
{
    public class Templates : ISerializationCallbackReceiver
    {
        public List<ProjectInfo> Projects { get; set; } = new List<ProjectInfo>();

        public bool IsDirty = false;

        public void OnAfterDeserialize()
        {
            if (Projects == null || Projects.Count <= 0) 
            {
                Debug.WriteLine("Template is null.");

                Projects = new List<ProjectInfo>()
                {
                    new ProjectInfo()
                    {
                        ProjectPath = Directory.GetCurrentDirectory() + @"/Templates/",
                        ProjectName = @"Empty Project",
                        Desc = "",

                    },
                    new ProjectInfo()
                    {
                        ProjectPath = Directory.GetCurrentDirectory() + @"/Templates/",
                        ProjectName = @"1st-Person Project",
                        Desc = "First person shotting game",
                        
                    },
                    new ProjectInfo()
                    {
                        ProjectPath = Directory.GetCurrentDirectory() + @"/Templates/",
                        ProjectName = @"3rd-Person Project",
                        Desc = "Third person shotting game",

                    },
                    new ProjectInfo()
                    {
                        ProjectPath = Directory.GetCurrentDirectory() + @"/Templates/",
                        ProjectName = @"Top-Down Project",
                        Desc = "Top donw game",

                    }
                };

                IsDirty = true;
            }
        }

        public void OnBeforeSerialize()
        {
            IsDirty = false;
        }
    }
}
