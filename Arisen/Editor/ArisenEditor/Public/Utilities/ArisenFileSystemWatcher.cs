using System;
using System.IO;

namespace ArisenEditor.Utilities;

public partial class ArisenFileSystemWatcher
{
    public static Action<object, FileSystemEventArgs> Changed;
    public static Action<object, RenamedEventArgs> Renamed;
    public static Action<object, FileSystemEventArgs> Created;
    public static Action<object, FileSystemEventArgs> Deleted;
    public static Action<object, ErrorEventArgs> Errored;
    
}