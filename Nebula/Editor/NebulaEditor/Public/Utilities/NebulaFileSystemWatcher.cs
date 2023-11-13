using System;
using System.IO;

namespace NebulaEditor.Utilities;

public partial class NebulaFileSystemWatcher
{
    public static Action<object, FileSystemEventArgs> Changed;
    public static Action<object, RenamedEventArgs> Renamed;
    public static Action<object, FileSystemEventArgs> Created;
    public static Action<object, FileSystemEventArgs> Deleted;
    public static Action<object, ErrorEventArgs> Errored;
    
}