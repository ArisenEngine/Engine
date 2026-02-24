using Serialization.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArisenEngine.Data.ProjectData
{
    public sealed class ProjectSettings : ISerializationCallbackReceiver
    {
        public string ProjectName = "New Project";
        public string PackageName = "Com.Arisen.Default";


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
