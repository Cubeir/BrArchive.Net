using System.Text;
using BrArchive;

namespace BrArchive.Net.Tests;

public class RoundTripTests
{
    [Fact]
    public void BasicRoundTrip_PreservesEntriesAndContent()
    {
        var bytes = BrArchiveBuilder.Create()
            .Add("terrain_texture.json", Encoding.UTF8.GetBytes("{\"a\":1}"))
            .Add("flipbook_textures.json", "[]", Encoding.UTF8)
            .Add("empty.json", Array.Empty<byte>())
            .ToArray();

        var archive = BrArchiveFile.Read(bytes);

        Assert.Equal(3, archive.Count);
        Assert.Equal("{\"a\":1}", archive["terrain_texture.json"].GetText());
        Assert.Equal("[]", archive["flipbook_textures.json"].GetText());
        Assert.Equal(0, archive["empty.json"].Length);
        Assert.False(archive["empty.json"].HasData);
        Assert.Equal(1u, archive.FormatVersion);
    }

    [Fact]
    public void Write_SortsEntriesByOrdinalName_ByDefault()
    {
        var archive = BrArchiveFile.Read(
            BrArchiveBuilder.Create()
                .Add("zzz.json", new byte[] { 1 })
                .Add("aaa.json", new byte[] { 1 })
                .Add("mmm.json", new byte[] { 1 })
                .ToArray());

        Assert.Equal(new[] { "aaa.json", "mmm.json", "zzz.json" }, archive.Names);
    }

    [Fact]
    public void Write_PreservesInsertionOrder_WhenSortEntriesDisabled()
    {
        var builder = BrArchiveBuilder.Create()
            .Add("zzz.json", new byte[] { 1 })
            .Add("aaa.json", new byte[] { 1 });
        builder.SortEntries = false;

        var archive = BrArchiveFile.Read(builder.ToArray());

        Assert.Equal(new[] { "zzz.json", "aaa.json" }, archive.Names);
    }

    [Theory]
    [InlineData("banner_flow.json")]
    [InlineData("café_texture.json")]
    [InlineData("  spaced.json")]
    [InlineData("a")]
    public void RoundTrip_PreservesVariousNames(string name)
    {
        var archive = BrArchiveFile.Read(
            BrArchiveBuilder.Create().Add(name, new byte[] { 9, 9, 9 }).ToArray());

        Assert.True(archive.Contains(name));
        Assert.Equal(new byte[] { 9, 9, 9 }, archive[name].Data);
    }

    [Fact]
    public void ManyEntries_AllRoundTripWithRandomContent()
    {
        var builder = BrArchiveBuilder.Create();
        var rnd = new Random(42);
        var expected = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        for (int i = 0; i < 50; i++)
        {
            string name = $"entry_{i:D3}.json";
            var data = new byte[rnd.Next(0, 2000)];
            rnd.NextBytes(data);
            expected[name] = data;
            builder.Add(name, data);
        }

        var archive = BrArchiveFile.Read(builder.ToArray());

        Assert.Equal(expected.Count, archive.Count);
        foreach (var (name, data) in expected)
            Assert.Equal(data, archive[name].Data);
    }
}

public class FormatLayoutTests
{
    [Fact]
    public void Write_ProducesExpectedMagicNumber()
    {
        var bytes = BrArchiveBuilder.Create().Add("a.json", new byte[] { 1 }).ToArray();
        byte[] expectedMagic = { 0x7D, 0x27, 0x25, 0xB1, 0xA0, 0x52, 0x70, 0x26 };

        Assert.Equal(expectedMagic, bytes.Take(8));
    }

    [Fact]
    public void Write_ProducesExpectedHeaderFields()
    {
        var bytes = BrArchiveBuilder.Create()
            .Add("a.json", new byte[] { 1 })
            .Add("b.json", new byte[] { 1, 2 })
            .ToArray();

        uint entryCount = BitConverter.ToUInt32(bytes, 8);
        uint version = BitConverter.ToUInt32(bytes, 12);

        Assert.Equal(2u, entryCount);
        Assert.Equal(1u, version);
    }

    [Fact]
    public void Write_ProducesExpectedEntryRecordLayout()
    {
        var bytes = BrArchiveBuilder.Create().Add("a.json", new byte[] { 1, 2, 3 }).ToArray();

        Assert.Equal(6, bytes[16]); // name length
        Assert.Equal("a.json", Encoding.UTF8.GetString(bytes, 17, 6));

        uint relativeOffset = BitConverter.ToUInt32(bytes, 16 + 1 + 247);
        uint dataLength = BitConverter.ToUInt32(bytes, 16 + 1 + 247 + 4);
        Assert.Equal(0u, relativeOffset);
        Assert.Equal(3u, dataLength);

        int dataBlockStart = 16 + 256;
        Assert.Equal(dataBlockStart + 3, bytes.Length);
        Assert.Equal(new byte[] { 1, 2, 3 }, bytes.Skip(dataBlockStart).Take(3));
    }

    [Fact]
    public void EntryRecord_IsExactly256Bytes()
    {
        // 1 (name length) + 247 (name buffer) + 4 (offset) + 4 (size) == 256
        Assert.Equal(256, 1 + BrArchiveFormat.NameBufferSize + 4 + 4);
        Assert.Equal(256, BrArchiveFormat.EntryRecordSize);
    }
}

public class ValidationTests
{
    [Fact]
    public void Add_MaxLengthName_IsAccepted()
    {
        string maxName = new string('a', BrArchiveFormat.MaxNameLengthBytes);
        var builder = BrArchiveBuilder.Create();

        var ex = Record.Exception(() => builder.Add(maxName, new byte[] { 1 }));

        Assert.Null(ex);
    }

    [Fact]
    public void Add_NameOneByteTooLong_Throws()
    {
        string tooLong = new string('a', BrArchiveFormat.MaxNameLengthBytes + 1);

        Assert.Throws<ArgumentException>(() => BrArchiveBuilder.Create().Add(tooLong, new byte[] { 1 }));
    }

    [Fact]
    public void Add_NameContainingNul_Throws()
    {
        Assert.Throws<ArgumentException>(() => BrArchiveBuilder.Create().Add("bad\0name.json", new byte[] { 1 }));
    }

    [Fact]
    public void Constructor_DuplicateNames_Throws()
    {
        Assert.Throws<ArgumentException>(() => new BrArchiveFile(new[]
        {
            new BrArchiveEntry("dup.json", new byte[] { 1 }),
            new BrArchiveEntry("dup.json", new byte[] { 2 }),
        }));
    }
}

public class CorruptionHandlingTests
{
    [Fact]
    public void Read_TooShortFile_Throws()
    {
        Assert.Throws<BrArchiveFormatException>(() => BrArchiveFile.Read(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public void Read_BadMagicNumber_Throws()
    {
        var bytes = BrArchiveBuilder.Create().Add("a.json", new byte[] { 1 }).ToArray();
        bytes[0] = 0x00;

        Assert.Throws<BrArchiveFormatException>(() => BrArchiveFile.Read(bytes));
    }

    [Fact]
    public void Read_TruncatedEntryTable_Throws()
    {
        var valid = BrArchiveBuilder.Create().Add("a.json", new byte[] { 1, 2, 3, 4, 5 }).ToArray();
        var truncated = valid.Take(20).ToArray();

        Assert.Throws<BrArchiveFormatException>(() => BrArchiveFile.Read(truncated));
    }

    [Fact]
    public void Read_TruncatedDataBlock_Throws()
    {
        var valid = BrArchiveBuilder.Create().Add("a.json", new byte[] { 1, 2, 3, 4, 5 }).ToArray();
        var truncated = valid.Take(valid.Length - 2).ToArray();

        Assert.Throws<BrArchiveFormatException>(() => BrArchiveFile.Read(truncated));
    }

    [Fact]
    public void Read_WithValidateMagicDisabled_SkipsMagicCheck()
    {
        var bytes = BrArchiveBuilder.Create().Add("a.json", new byte[] { 1 }).ToArray();
        bytes[0] = 0x00;

        var archive = BrArchiveFile.Read(bytes, new BrArchiveReadOptions { ValidateMagic = false });

        Assert.Single(archive.Entries);
    }
}

public class BuilderEditingTests
{
    [Fact]
    public void ToBuilder_AllowsRemovingAndAddingEntries()
    {
        var original = BrArchiveFile.Read(
            BrArchiveBuilder.Create()
                .Add("keep.json", Encoding.UTF8.GetBytes("keep"))
                .Add("remove.json", Encoding.UTF8.GetBytes("bye"))
                .ToArray());

        var edited = original.ToBuilder()
            .Remove("remove.json")
            .Add("new.json", Encoding.UTF8.GetBytes("hello"))
            .Build();

        Assert.Equal(2, edited.Count);
        Assert.True(edited.Contains("keep.json"));
        Assert.False(edited.Contains("remove.json"));
        Assert.Equal("hello", edited["new.json"].GetText());
    }

    [Fact]
    public void Remove_IsFluent_AndNoOpForMissingEntry()
    {
        var builder = BrArchiveBuilder.Create().Add("a.json", new byte[] { 1 });

        var result = builder.Remove("does-not-exist.json").Add("b.json", new byte[] { 2 });

        Assert.Same(builder, result);
        Assert.Equal(2, builder.Count);
    }
}

public class DirectoryAndFileIoTests : IDisposable
{
    private readonly string _tempDir;

    public DirectoryAndFileIoTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "brarchive_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void AddDirectory_NonRecursive_OnlyIncludesTopLevelFiles()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "subdir"));
        File.WriteAllText(Path.Combine(_tempDir, "one.json"), "1");
        File.WriteAllText(Path.Combine(_tempDir, "subdir", "two.json"), "2");

        var archive = BrArchiveBuilder.FromDirectory(_tempDir).Build();

        Assert.Equal(1, archive.Count);
        Assert.True(archive.Contains("one.json"));
    }

    [Fact]
    public void AddDirectory_Recursive_UsesSlashSeparatedRelativeNames()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "subdir"));
        File.WriteAllText(Path.Combine(_tempDir, "one.json"), "1");
        File.WriteAllText(Path.Combine(_tempDir, "subdir", "two.json"), "22");

        var archive = BrArchiveBuilder.FromDirectory(_tempDir, recursive: true).Build();

        Assert.Equal(2, archive.Count);
        Assert.True(archive.Contains("subdir/two.json"));
        Assert.Equal("22", archive["subdir/two.json"].GetText());
    }

    [Fact]
    public void WriteFile_Then_ReadFile_RoundTrips()
    {
        string path = Path.Combine(_tempDir, "out.brarchive");
        var archive = BrArchiveBuilder.Create().Add("a.json", Encoding.UTF8.GetBytes("hi")).Build();

        archive.WriteFile(path);
        var reloaded = BrArchiveFile.ReadFile(path);

        Assert.Equal("hi", reloaded["a.json"].GetText());
    }

    [Fact]
    public async Task WriteFileAsync_Then_ReadFileAsync_RoundTrips()
    {
        string path = Path.Combine(_tempDir, "out_async.brarchive");
        var archive = BrArchiveBuilder.Create().Add("x.json", Encoding.UTF8.GetBytes("y")).Build();

        await archive.WriteFileAsync(path);
        var reloaded = await BrArchiveFile.ReadFileAsync(path);

        Assert.Equal("y", reloaded["x.json"].GetText());
    }
}
