// See https://aka.ms/new-console-template for more information
using NebulaEngine.FileSystem;

Console.WriteLine("Hello, World!");

DirectoryUtilities.CopyDirectoryRecursively("E:\\TechCenter\\NebulaEngine\\Nebula\\Editor\\NebulaEditor\\Templates\\1st-Person Project", "E:\\TechCenter\\NebulaEngine\\CopyTest", (file, sourceDir, destionationDir) =>
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