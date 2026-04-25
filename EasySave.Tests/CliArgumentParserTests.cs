using EasySave.Console;

namespace EasySave.Tests;

public sealed class CliArgumentParserTests
{
    [Fact]
    public void ParseRangeReturnsExpectedIndexes()
    {
        var parser = new CliArgumentParser();

        var result = parser.Parse("1-3", existingJobCount: 5);

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2, 3], result.JobIndexes);
    }

    [Fact]
    public void ParseListReturnsExpectedIndexes()
    {
        var parser = new CliArgumentParser();

        var result = parser.Parse("1;3", existingJobCount: 5);

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 3], result.JobIndexes);
    }

    [Fact]
    public void ParseRejectsDuplicates()
    {
        var parser = new CliArgumentParser();

        var result = parser.Parse("1;1", existingJobCount: 5);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ParseRejectsOutOfRangeIndexes()
    {
        var parser = new CliArgumentParser();

        var result = parser.Parse("1-6", existingJobCount: 5);

        Assert.False(result.IsSuccess);
    }
}
