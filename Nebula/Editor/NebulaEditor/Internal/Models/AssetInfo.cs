using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEditor.Models
{
    internal enum AssetType
    {
        Unknow,
        Folder,
        File
    }

    internal class AssetInfo
    {
        internal AssetType type = AssetType.Unknow;
    }
}
