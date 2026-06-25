using BookLoggerApp.Infrastructure.Services;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Security;

public class ZipBombProtectionTests
{
    [Fact]
    public void CopyStreamWithLimit_throws_when_actual_bytes_exceed_limit()
    {
        // Guard must count bytes actually written, not the declared header size:
        // 1000 real bytes against a 512 limit must fail.
        using var src = new MemoryStream(new byte[1000]);
        using var dest = new MemoryStream();

        Action act = () => ImportExportService.CopyStreamWithLimit(src, dest, 512);

        act.Should().Throw<IOException>().WithMessage("*Zip Bomb*");
    }

    [Fact]
    public void CopyStreamWithLimit_copies_when_within_limit_and_returns_byte_count()
    {
        var data = new byte[300];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i % 251);
        }
        using var src = new MemoryStream(data);
        using var dest = new MemoryStream();

        long written = ImportExportService.CopyStreamWithLimit(src, dest, 512);

        written.Should().Be(300);
        dest.ToArray().Should().Equal(data);
    }
}
