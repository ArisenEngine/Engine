using Serialization.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEngine.Data.ProjectData
{
    public class ProjectSettings : ISerializationCallbackReceiver
    {
        public string ProjectName = "New Project";
        public string PackageName = "Com.Nebula.Default";


        public void OnAfterDeserialize()
        {
            throw new NotImplementedException();
        }

        public void OnBeforeSerialize()
        {
            throw new NotImplementedException();
        }
    }
}
