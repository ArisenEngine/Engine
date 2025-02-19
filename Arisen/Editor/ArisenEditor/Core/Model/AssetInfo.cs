using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArisenEditor.Models
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
