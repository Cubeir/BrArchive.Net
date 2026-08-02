# BrArchive.Net

If you make Minecraft Bedrock Edition add-ons: `.brarchive` is the format Mojang
uses to bundle a resource/behavior pack's JSON files into single per-directory
archives for faster loading. This library lets you look inside existing ones or
build your own - as a .NET library, or via the ready-to-run
[CLI tool](#get-the-cli-tool) below if you just want a command-line utility and
don't write code.

A dependency-free, fully managed .NET library for reading, writing, and inspecting
Minecraft Bedrock Edition **`.brarchive`** files - the archive format Mojang introduced
to bundle the many small JSON files inside built-in resource/behavior packs
(`textures.brarchive`, `sounds.brarchive`, etc.) into a single file per directory,
cutting down on filesystem I/O.

There's no official public specification for this format. This library is an
independent, clean-room C# implementation based on the on-disk layout as understood
from community reverse-engineering, cross-checked against two existing open-source
implementations (one Rust, one C - see [`THIRD-PARTY-NOTICES.md`](https://github.com/Cubeir/BrArchive.Net/blob/master/THIRD-PARTY-NOTICES.md)
for full credit). No code was copied from either project.

- Targets `netstandard2.0`, `net8.0`, and `net10.0` - works from .NET Framework 4.6.1+
  through the latest .NET, including Unity and Xamarin/MAUI via netstandard2.0.
- Zero external dependencies.
- Read, write, edit, and query archives. Pack a whole directory in one call.
- Defensive parsing: corrupt/truncated files raise a clear `BrArchiveFormatException`
  instead of crashing or reading garbage.

[![Build Status](https://img.shields.io/github/actions/workflow/status/Cubeir/BrArchive.Net/release.yml?branch=master&label=build&style=flat-square&color=2ea44f)](https://github.com/Cubeir/BrArchive.Net/actions)
[![Last Commit](https://img.shields.io/github/last-commit/Cubeir/BrArchive.Net?style=flat-square&color=blue)](https://github.com/Cubeir/BrArchive.Net/commits/master)
[![Repo Size](https://img.shields.io/github/repo-size/Cubeir/BrArchive.Net?style=flat-square&color=informational)](https://github.com/Cubeir/BrArchive.Net)
[![License](https://img.shields.io/github/license/Cubeir/BrArchive.Net?style=flat-square&color=yellow)](https://github.com/Cubeir/BrArchive.Net/blob/master/LICENSE)
[![NuGet](https://img.shields.io/nuget/v/BrArchive.Net?style=flat-square&color=blueviolet)](https://www.nuget.org/packages/BrArchive.Net)
[![Issues](https://img.shields.io/github/issues/Cubeir/BrArchive.Net?style=flat-square&color=critical)](https://github.com/Cubeir/BrArchive.Net/issues)

## Install

```bash
dotnet add package BrArchive.Net
```

## Quick start

### Reading an archive

```csharp
using BrArchive;

var archive = BrArchiveFile.ReadFile("textures.brarchive");

Console.WriteLine($"{archive.Count} entries, format version {archive.FormatVersion}");

foreach (var entry in archive.Entries)
    Console.WriteLine($"{entry.Name} ({entry.Length} bytes)");
```

### Querying specific entries

```csharp
if (archive.TryGetEntry("terrain_texture.json", out var entry))
{
    string json = entry!.GetText(); // defaults to UTF-8
}

// Or, if you're confident it exists:
byte[] bytes = archive["flipbook_textures.json"].Data;

bool exists = archive.Contains("some_file.json");
```

### Creating a new archive from scratch

```csharp
using BrArchive;
using System.Text;

BrArchiveBuilder.Create()
    .Add("terrain_texture.json", jsonBytes)
    .Add("flipbook_textures.json", "[]", Encoding.UTF8)
    .AddFile("icon.png", "path/to/icon.png")
    .SaveFile("output.brarchive");
```

### Packing a whole directory

```csharp
// Mirrors how vanilla archives work: one .brarchive per directory.
BrArchiveBuilder.FromDirectory("./textures")
    .SaveFile("textures.brarchive");

// Or, opt in to recursive packing with '/'-separated relative names:
BrArchiveBuilder.FromDirectory("./textures", recursive: true)
    .SaveFile("textures.brarchive");
```

### Editing an existing archive

```csharp
var archive = BrArchiveFile.ReadFile("textures.brarchive");

archive.ToBuilder()
    .Remove("old_entry.json")
    .Add("new_entry.json", newBytes)
    .SaveFile("textures.brarchive"); // overwrite in place
```

### Async I/O

```csharp
var archive = await BrArchiveFile.ReadFileAsync("textures.brarchive");
await archive.WriteFileAsync("copy.brarchive");
```

## Get the CLI tool

The `samples/BrArchive.Net.Cli` project in this repository is a small command-line
tool built on top of the library, useful on its own and as a reference for the API:

- **Download a ready-to-run build** - grab the zip for your OS from the
  [Releases page](https://github.com/Cubeir/BrArchive.Net/releases), unzip it,
  and run it directly. No .NET SDK required.
- **Or, if you already have the .NET SDK**, build it from source:
  ```bash
  git clone https://github.com/Cubeir/BrArchive.Net.git
  cd BrArchive.Net
  dotnet run --project samples/BrArchive.Net.Cli -- list mypack.brarchive
  ```

The `samples/BrArchive.Net.Cli` project is also useful as a reference for the
library's API if you're integrating it into your own code.

```bash
brarchive list textures.brarchive
brarchive info textures.brarchive
brarchive extract textures.brarchive ./extracted
brarchive pack ./my_textures textures.brarchive --recursive
```

## Format notes

- Every valid file starts with a fixed 8-byte magic number, followed by a 4-byte
  little-endian entry count and a 4-byte little-endian format version (the only
  known value is `1`).
- Each entry is a fixed 256-byte record: a 1-byte name length, a 247-byte
  zero-padded UTF-8 name buffer, a 4-byte little-endian relative content offset,
  and a 4-byte little-endian content length.
- The full byte-level layout, with field-by-field offsets, is documented in the
  XML doc comments on `BrArchiveFormat` in
  [`src/BrArchive.Net/BrArchiveFormat.cs`](https://github.com/Cubeir/BrArchive.Net/blob/master/src/BrArchive.Net/BrArchiveFormat.cs).
- Some entries in the wild have a content length of zero - this library surfaces
  those as an entry with empty `Data` and `HasData == false`, rather than guessing.
- Because this is a community-derived understanding of an undocumented format
  rather than an official spec, treat any single-project reverse-engineering
  (including this one) with appropriate skepticism, and please open an issue if
  you find a real-world archive this library mis-parses.

### Example: how a resource pack's archives are laid out

Minecraft's Bedrock resource pack usually mirrors its own folder structure under `__brarchive/`,
with one archive per directory - a nested subdirectory gets its own *separate*
archive file, not a combined one:

```
resource_pack/
├── __brarchive/
│   ├── sounds.brarchive
│   ├── textures.brarchive
│   └── textures/
│       └── entity/
│           └── banner.brarchive      <- a separate archive for this subfolder
├── sounds/
└── textures/
    └── entity/
        └── banner/*.png
```

`BrArchiveBuilder.FromDirectory(path)` (non-recursive) produces exactly one of
these archives at a time - that's the real atomic operation. Reproducing a whole
`__brarchive` tree for an entire pack is a straightforward composition of that
single call, one directory level at a time:

```csharp
void PackTree(string sourceDir, string archiveOutputDir)
{
    Directory.CreateDirectory(archiveOutputDir);
    string archiveName = Path.GetFileName(sourceDir.TrimEnd(Path.DirectorySeparatorChar)) + ".brarchive";
    BrArchiveBuilder.FromDirectory(sourceDir).SaveFile(Path.Combine(archiveOutputDir, archiveName));

    foreach (var subDir in Directory.GetDirectories(sourceDir))
        PackTree(subDir, Path.Combine(archiveOutputDir, Path.GetFileName(subDir)));
}
```

This library intentionally stops at that single-archive primitive rather than
shipping a full pack-replicating tool - producing an exact `__brarchive` tree
means guessing at Mojang's own (undocumented) packing *policy*, not just the
file format, so that's left to whatever you build on top.

## Building from source

```bash
git clone https://github.com/Cubeir/BrArchive.Net.git
cd BrArchive.Net
dotnet build
dotnet test
```

## Contributing / License

Contributions welcome - please open an issue or PR. Licensed under the
[MIT License](https://github.com/Cubeir/BrArchive.Net/blob/master/LICENSE). See
[`THIRD-PARTY-NOTICES.md`](https://github.com/Cubeir/BrArchive.Net/blob/master/THIRD-PARTY-NOTICES.md)
for credit to the prior open-source work this library's understanding of the
format is based on, and [`CHANGELOG.md`](https://github.com/Cubeir/BrArchive.Net/blob/master/CHANGELOG.md)
for release history.