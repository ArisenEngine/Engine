
using System.Text;

namespace ArisenEngine.FileSystem
{
    public  static partial class FileSystemUtilities
    {
        /// <summary>
        /// Copy directory recursively, while overrideAction returns true means that user want to hanle files fully by self 
        /// </summary>
        /// <param name="sourceDir"></param>
        /// <param name="destinationDir"></param>
        /// <param name="overrideAction"></param>
        public static void CopyDirectoryRecursively(string sourceDir, string destinationDir, Func<FileInfo, DirectoryInfo, DirectoryInfo, bool>? overrideAction)
        {
            CopyDirectory(sourceDir, destinationDir, overrideAction);
        }

        public static void CopyDirectory(string sourceDir, string destinationDir, Func<FileInfo, DirectoryInfo, DirectoryInfo, bool>? overrideAction, bool recursive = true)
        {
            // Get information about the source directory
            var dir = new DirectoryInfo(sourceDir);

            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            var destination = new DirectoryInfo(destinationDir);

            // Check if the source directory exists
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            // Cache directories before we start copying
            DirectoryInfo[] dirs = dir.GetDirectories();

            // Create the destination directory
            Directory.CreateDirectory(destinationDir);

            // Get the files in the source directory and copy to the destination directory
            var files = dir.GetFiles().ToList();
            for (var i = 0; i < files.Count; ++i)
            {
                var file = files[i];

                if (overrideAction != null && overrideAction.Invoke(file, dir, destination))
                {
                    continue;
                }

                string targetFilePath = Path.Combine(destinationDir, file.Name);

                file.CopyTo(targetFilePath);
            }

            // If recursive and copying subdirectories, recursively call this method
            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, overrideAction);
                }
            }
        }
        
        
        static ReaderWriterLock locker = new ReaderWriterLock();
        public static void AppendTextToFile(string text, string fileFullPath)
        {
            // Use Encoding.UTF8 or another encoding based on your requirements
            Encoding encoding = Encoding.UTF8;

            locker.AcquireWriterLock(Int32.MaxValue);
            try
            {
                if (!File.Exists(fileFullPath))
                {
                    File.Create(fileFullPath).Close();
                }


                using (var sw = new StreamWriter(fileFullPath, true, encoding))
                {
                    sw.WriteLine(text);
                }
                
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                locker.ReleaseWriterLock();
            }
        }

        public static long GetFolderSize(string folderPath)
        {
            long size = 0;
            try
            {
                var dirInfo = new DirectoryInfo(folderPath);
                if (dirInfo.Exists)
                {
                    var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        size += file.Length;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            return size;
        }
        
        public static void DeleteDirectory(string directoryPath, List<string> skipFolders, List<string> excludedExtensions)
        {
            var directoryInfo = new DirectoryInfo(directoryPath);
            if (skipFolders.Contains(directoryInfo.Name))
            {
                return;
            }
    
            bool hasSkipFile = false;
            foreach (var file in Directory.GetFiles(directoryPath))
            {
                var fileInfo = new FileInfo(file);
                if (excludedExtensions.Count > 0 && excludedExtensions.Contains(fileInfo.Extension))
                {
                    hasSkipFile = true;
                    continue;
                }
                
                File.Delete(file);
            }
            
            foreach (var directory in Directory.GetDirectories(directoryPath))
            {
                DeleteDirectory(directory, skipFolders, excludedExtensions);
            }
    
            if (hasSkipFile)
            {
                return;
            }

            Directory.Delete(directoryPath);
            
        }
    }
}
