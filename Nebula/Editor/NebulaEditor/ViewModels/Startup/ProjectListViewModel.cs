using NebulaEditor.Models.Startup;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEditor.ViewModels.Startup
{
    public class ProjectListViewModel : ViewModelBase
    {
        public List<ProjectInfo>? ProjectsList { get; set; } = new List<ProjectInfo>();
    }
}
