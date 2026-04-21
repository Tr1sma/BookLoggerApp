using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Services;

public class FileSystemAdapterTests : IDisposable
{
    private readonly FileSystemAdapter _adapter = new();
    private readonly string _tempDir;

    public FileSystemAdapterTests()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FileSystemAdapterTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort cleanup */ }
        }
    }

    private string MkPath(string name) => System.IO.Path.Combine(_tempDir, name);

    [Fact]
    public async Task ReadWriteAllTextAsync_RoundTrip()
    {
        var file = MkPath("text.txt");
        await _adapter.WriteAllTextAsync(file, "Hello World");

        var content = await _adapter.ReadAllTextAsync(file);

        content.Should().Be("Hello World");
    }

    [Fact]
    public async Task ReadWriteAllBytesAsync_RoundTrip()
    {
        var file = MkPath("bytes.bin");
        byte[] data = { 1, 2, 3, 4, 5 };
        await _adapter.WriteAllBytesAsync(file, data);

        var result = await _adapter.ReadAllBytesAsync(file);

        result.Should().Equal(data);
    }

    [Fact]
    public void FileExists_ExistingFile_ReturnsTrue()
    {
        var file = MkPath("existing.txt");
        File.WriteAllText(file, "x");

        _adapter.FileExists(file).Should().BeTrue();
    }

    [Fact]
    public void FileExists_MissingFile_ReturnsFalse()
    {
        _adapter.FileExists(MkPath("missing.txt")).Should().BeFalse();
    }

    [Fact]
    public void DirectoryExists_ExistingDir_ReturnsTrue()
    {
        _adapter.DirectoryExists(_tempDir).Should().BeTrue();
    }

    [Fact]
    public void DirectoryExists_MissingDir_ReturnsFalse()
    {
        _adapter.DirectoryExists(MkPath("nosuchdir")).Should().BeFalse();
    }

    [Fact]
    public void CombinePath_JoinsSegments()
    {
        var combined = _adapter.CombinePath("a", "b", "c.txt");

        combined.Should().Be(System.IO.Path.Combine("a", "b", "c.txt"));
    }

    [Fact]
    public void CreateDirectory_CreatesMissingDirectory()
    {
        var sub = MkPath("newsub");

        _adapter.CreateDirectory(sub);

        Directory.Exists(sub).Should().BeTrue();
    }

    [Fact]
    public void CreateDirectory_AlreadyExists_DoesNotThrow()
    {
        var sub = MkPath("already");
        Directory.CreateDirectory(sub);

        Action act = () => _adapter.CreateDirectory(sub);

        act.Should().NotThrow();
    }

    [Fact]
    public void DeleteFile_ExistingFile_Removes()
    {
        var file = MkPath("todelete.txt");
        File.WriteAllText(file, "x");

        _adapter.DeleteFile(file);

        File.Exists(file).Should().BeFalse();
    }

    [Fact]
    public void CopyFile_CopiesContent()
    {
        var src = MkPath("src.txt");
        var dst = MkPath("dst.txt");
        File.WriteAllText(src, "content");

        _adapter.CopyFile(src, dst);

        File.Exists(dst).Should().BeTrue();
        File.ReadAllText(dst).Should().Be("content");
    }

    [Fact]
    public void CopyFile_OverwriteTrue_ReplacesTarget()
    {
        var src = MkPath("src.txt");
        var dst = MkPath("dst.txt");
        File.WriteAllText(src, "new");
        File.WriteAllText(dst, "old");

        _adapter.CopyFile(src, dst, overwrite: true);

        File.ReadAllText(dst).Should().Be("new");
    }

    [Fact]
    public void CopyFile_OverwriteFalseWithExistingTarget_Throws()
    {
        var src = MkPath("src.txt");
        var dst = MkPath("dst.txt");
        File.WriteAllText(src, "new");
        File.WriteAllText(dst, "old");

        Action act = () => _adapter.CopyFile(src, dst, overwrite: false);

        act.Should().Throw<IOException>();
    }

    [Fact]
    public void OpenWrite_ReturnsWritableStream()
    {
        var file = MkPath("stream.bin");

        using (var stream = _adapter.OpenWrite(file))
        {
            stream.CanWrite.Should().BeTrue();
            var bytes = new byte[] { 0xAA, 0xBB };
            stream.Write(bytes, 0, bytes.Length);
        }

        File.ReadAllBytes(file).Should().Equal(0xAA, 0xBB);
    }
}
