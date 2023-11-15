using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineLib.FileSystem
{
    public static class DirectoryUtilities
    {
        /// <summary>
        /// Copy directory recursively, while overrideAction returns true means that user want to hanle files fully by self 
        /// </summary>
        /// <param name="sourceDir"></param>
        /// <param name="destinationDir"></param>
        /// <param name="overrideAction"></param>
        public static void CopyDirectoryRecursively(string sourceDir, string destinationDir, Func<FileInfo, DirectoryInfo, DirectoryInfo, bool> overrideAction = null)
        {
            CopyDirectory(sourceDir, destinationDir, true, overrideAction);
        }

        public static void CopyDirectory(string sourceDir, string destinationDir, bool recursive = true, Func<FileInfo, DirectoryInfo, DirectoryInfo, bool> overrideAction = null)
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
                    CopyDirectory(subDir.FullName, newDestinationDir, true, overrideAction);
                }
            }
        }
    }
}
