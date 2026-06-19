using BookLoggerApp.Infrastructure.Data;
using FluentAssertions;
using Xunit;

namespace BookLoggerApp.Tests.Infrastructure;

public class MigrationRecoveryTests
{
    [Theory]
    [InlineData("duplicate column name: IsHiddenByEntitlement")]
    [InlineData("SQLite Error 1: 'table \"Books\" already exists'.")]
    [InlineData("index \"IX_Books_Title\" already exists")]
    [InlineData("trigger trg_books_audit already exists")]
    [InlineData("view v_stats already exists")]
    public void IsSchemaAlreadyAppliedError_true_for_recoverable_ddl_errors(string message)
    {
        MigrationRecovery.IsSchemaAlreadyAppliedError(new Exception(message)).Should().BeTrue();
    }

    [Theory]
    [InlineData("A file with that name already exists.")] // non-DDL "already exists" — must NOT recover
    [InlineData("Sequence already exists")]
    [InlineData("no such column: ProductVersion")]
    [InlineData("UNIQUE constraint failed: Books.Id")]
    [InlineData("database is locked")]
    public void IsSchemaAlreadyAppliedError_false_for_unrelated_errors(string message)
    {
        MigrationRecovery.IsSchemaAlreadyAppliedError(new Exception(message)).Should().BeFalse();
    }

    [Fact]
    public void IsSchemaAlreadyAppliedError_walks_inner_exception_chain()
    {
        var inner = new Exception("duplicate column name: X");
        var outer = new InvalidOperationException("EF wrapper", inner);
        MigrationRecovery.IsSchemaAlreadyAppliedError(outer).Should().BeTrue();
    }
}
