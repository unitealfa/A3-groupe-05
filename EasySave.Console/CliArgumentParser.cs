namespace EasySave.Console;

public sealed class CliArgumentParser
{
    public CliParseResult Parse(string argument, int existingJobCount, int maxJobCount = 5)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            return CliParseResult.Failure("CLI argument is required.");
        }

        if (string.Equals(argument, "all", StringComparison.OrdinalIgnoreCase))
        {
            return existingJobCount == 0
                ? CliParseResult.Failure("No backup jobs are configured.")
                : CliParseResult.Success(Enumerable.Range(1, existingJobCount));
        }

        var indexes = argument.Contains('-', StringComparison.Ordinal)
            ? ParseRange(argument)
            : ParseList(argument);

        if (!indexes.IsSuccess)
        {
            return indexes;
        }

        var distinctIndexes = indexes.JobIndexes.Distinct().ToList();
        if (distinctIndexes.Count != indexes.JobIndexes.Count)
        {
            return CliParseResult.Failure("Duplicate job indexes are not allowed.");
        }

        if (distinctIndexes.Any(index => index < 1 || index > maxJobCount))
        {
            return CliParseResult.Failure($"Job indexes must be between 1 and {maxJobCount}.");
        }

        if (distinctIndexes.Any(index => index > existingJobCount))
        {
            return CliParseResult.Failure("One or more requested backup jobs do not exist.");
        }

        return CliParseResult.Success(distinctIndexes);
    }

    private static CliParseResult ParseRange(string argument)
    {
        var parts = argument.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var start) || !int.TryParse(parts[1], out var end) || start > end)
        {
            return CliParseResult.Failure("Invalid range format. Expected example: 1-3.");
        }

        return CliParseResult.Success(Enumerable.Range(start, end - start + 1));
    }

    private static CliParseResult ParseList(string argument)
    {
        var parts = argument.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return CliParseResult.Failure("Invalid list format. Expected example: 1;3.");
        }

        var indexes = new List<int>();
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var index))
            {
                return CliParseResult.Failure("Invalid list format. Expected example: 1;3.");
            }

            indexes.Add(index);
        }

        return CliParseResult.Success(indexes);
    }
}

public sealed class CliParseResult
{
    private CliParseResult(bool isSuccess, IReadOnlyList<int> jobIndexes, string errorMessage)
    {
        IsSuccess = isSuccess;
        JobIndexes = jobIndexes;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public IReadOnlyList<int> JobIndexes { get; }

    public string ErrorMessage { get; }

    public static CliParseResult Success(IEnumerable<int> jobIndexes)
    {
        return new CliParseResult(true, jobIndexes.ToList(), string.Empty);
    }

    public static CliParseResult Failure(string errorMessage)
    {
        return new CliParseResult(false, [], errorMessage);
    }
}
