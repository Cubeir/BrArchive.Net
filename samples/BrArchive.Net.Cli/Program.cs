using System;
using System.IO;
using System.Linq;
using BrArchive;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

try
{
    switch (args[0])
    {
        case "list":
            return CommandList(args);
        case "info":
            return CommandInfo(args);
        case "extract":
            return CommandExtract(args);
        case "pack":
            return CommandPack(args);
        default:
            PrintUsage();
            return 1;
    }
}
catch (BrArchiveFormatException ex)
{
    Console.Error.WriteLine($"error: not a valid .brarchive file - {ex.Message}");
    return 1;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException or DirectoryNotFoundException)
{
    Console.Error.WriteLine($"error: {ex.Message}");
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("""
        brarchive - a tool for the Minecraft Bedrock Edition .brarchive format (BrArchive.Net)

        Usage:
          brarchive list <file.brarchive>
          brarchive info <file.brarchive>
          brarchive extract <file.brarchive> <output-directory>
          brarchive pack <input-directory> <file.brarchive> [--recursive]

        Examples:
          brarchive list textures.brarchive
          brarchive extract textures.brarchive ./extracted
          brarchive pack ./my_textures textures.brarchive --recursive
        """);
}

static int CommandList(string[] args)
{
    if (args.Length < 2) { PrintUsage(); return 1; }

    var archive = BrArchiveFile.ReadFile(args[1]);
    foreach (var entry in archive.Entries)
        Console.WriteLine($"{entry.Length,10:N0}  {entry.Name}");

    Console.WriteLine($"\n{archive.Count} entr{(archive.Count == 1 ? "y" : "ies")}, format version {archive.FormatVersion}");
    return 0;
}

static int CommandInfo(string[] args)
{
    if (args.Length < 2) { PrintUsage(); return 1; }

    var archive = BrArchiveFile.ReadFile(args[1]);
    long totalBytes = archive.Entries.Sum(e => (long)e.Length);
    int emptyCount = archive.Entries.Count(e => !e.HasData);

    Console.WriteLine($"File:            {args[1]}");
    Console.WriteLine($"Format version:  {archive.FormatVersion}");
    Console.WriteLine($"Entries:         {archive.Count:N0}");
    Console.WriteLine($"Entries w/ data: {archive.Count - emptyCount:N0}");
    Console.WriteLine($"Empty entries:   {emptyCount:N0}");
    Console.WriteLine($"Total content:   {totalBytes:N0} bytes");
    return 0;
}

static int CommandExtract(string[] args)
{
    if (args.Length < 3) { PrintUsage(); return 1; }

    var archive = BrArchiveFile.ReadFile(args[1]);
    string outputDir = args[2];
    Directory.CreateDirectory(outputDir);

    foreach (var entry in archive.Entries)
    {
        // Entry names may contain '/' if they were packed recursively; recreate that structure.
        string destination = Path.Combine(outputDir, entry.Name.Replace('/', Path.DirectorySeparatorChar));
        string? destinationDir = Path.GetDirectoryName(destination);
        if (!string.IsNullOrEmpty(destinationDir))
            Directory.CreateDirectory(destinationDir);

        File.WriteAllBytes(destination, entry.Data);
        Console.WriteLine($"extracted  {entry.Name}");
    }

    Console.WriteLine($"\n{archive.Count} entr{(archive.Count == 1 ? "y" : "ies")} extracted to {outputDir}");
    return 0;
}

static int CommandPack(string[] args)
{
    if (args.Length < 3) { PrintUsage(); return 1; }

    string inputDir = args[1];
    string outputFile = args[2];
    bool recursive = args.Contains("--recursive");

    var builder = BrArchiveBuilder.FromDirectory(inputDir, recursive);
    builder.SaveFile(outputFile);

    Console.WriteLine($"packed {builder.Count} entr{(builder.Count == 1 ? "y" : "ies")} from {inputDir} into {outputFile}");
    return 0;
}
