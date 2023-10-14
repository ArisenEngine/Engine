using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEditor.Interface
{
    public interface ISerializable
    {
        public void OnSerizlized();

        public void OnDeserizlized();

    }
}
