# BrArchive.Net

A dependency-free, fully managed .NET library for reading, writing, and inspecting
Minecraft Bedrock Edition **`.brarchive`** files - the archive format Mojang introduced
to bundle the many small JSON files inside built-in resource/behavior packs
(`textures.brarchive`, `sounds.brarchive`, etc.) into a single file per directory,
cutting down on filesystem I/O.

There's no official public specification for this format. This library is an
independent, clean-room C# implementation based on the on-disk layout as understood
from community reverse-engineering, cross-checked against two existing open-source
implementations (one Rust, one C - see [`THIRD-PARTY-NOTICES.md`](./THIRD-PARTY-NOTICES.md)
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

## The CLI sample tool

The `samples/BrArchive.Net.Cli` project in this repository is a small command-line
tool built on top of the library, useful on its own and as a reference for the API:

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
  [`src/BrArchive.Net/BrArchiveFormat.cs`](./src/BrArchive.Net/BrArchiveFormat.cs).
- Some entries in the wild have a content length of zero - this library surfaces
  those as an entry with empty `Data` and `HasData == false`, rather than guessing.
- Because this is a community-derived understanding of an undocumented format
  rather than an official spec, treat any single-project reverse-engineering
  (including this one) with appropriate skepticism, and please open an issue if
  you find a real-world archive this library mis-parses.

## Building from source

```bash
git clone https://github.com/YOUR-GITHUB-USERNAME/BrArchive.Net.git
cd BrArchive.Net
dotnet build
dotnet test
```

## Contributing / License

Contributions welcome - please open an issue or PR. Licensed under the
[MIT License](./LICENSE). See [`THIRD-PARTY-NOTICES.md`](./THIRD-PARTY-NOTICES.md)
for credit to the prior open-source work this library's understanding of the
format is based on.
