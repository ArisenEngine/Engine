// See https://aka.ms/new-console-template for more information
using ArisenEngine.FileSystem;

Console.WriteLine("Hello, World!");

FileSystemUtilities.CopyDirectoryRecursively("E:\\TechCenter\\ArisenEngine\\Arisen\\Editor\\ArisenEditor\\Templates\\1st-Person Project", "E:\\TechCenter\\ArisenEngine\\CopyTest", (file, sourceDir, destionationDir) =>
{
    var fileName = file.Name;
    if (file.Extension == ".sln")
    {
        fileName = destionationDir.Name + ".sln";
    }

    string targetFilePath = Path.Combine(destionationDir.FullName, file.Name);

    file.CopyTo(targetFilePath);

    return true;
});