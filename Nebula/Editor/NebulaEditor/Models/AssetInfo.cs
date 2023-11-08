using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NebulaEditor.Models
{
    public enum AssetType
    {
        Unknow,
        Folder,
        File
    }

    public class AssetInfo
    {
        public AssetType type = AssetType.Unknow;
    }
}
