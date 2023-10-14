using Serialization.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEditor.Models.Startup
{
    public class Templates : ISerializationCallbackReceiver
    {
        public List<ProjectInfo> Projects { get; set; }

        public void OnAfterDeserialize()
        {
            
        }

        public void OnBeforeSerialize()
        {
            
        }
    }
}
