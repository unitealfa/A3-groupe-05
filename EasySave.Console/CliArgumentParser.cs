namespace EasySave.Console;

public sealed class CliArgumentParser
{
    public CliParseResult Parse(
        string argument,
        int existingJobCount,
        int maxJobCount = 5,
        Func<string, string>? localize = null)
    {
        localize ??= GetDefaultText;

        if (string.IsNullOrWhiteSpace(argument))
        {
            return CliParseResult.Failure(localize("CliArgumentRequired"));
        }

        if (string.Equals(argument, "all", StringComparison.OrdinalIgnoreCase))
        {
            return existingJobCount == 0
                ? CliParseResult.Failure(localize("NoJobs"))
                : CliParseResult.Success(Enumerable.Range(1, existingJobCount));
        }

        var indexes = argument.Contains('-', StringComparison.Ordinal)
            ? ParseRange(argument, localize)
            : ParseList(argument, localize);

        if (!indexes.IsSuccess)
        {
            return indexes;
        }

        var distinctIndexes = indexes.JobIndexes.Distinct().ToList();
        if (distinctIndexes.Count != indexes.JobIndexes.Count)
        {
            return CliParseResult.Failure(localize("CliDuplicateIndexes"));
        }

        if (distinctIndexes.Any(index => index < 1 || index > maxJobCount))
        {
            return CliParseResult.Failure(localize("CliIndexOutOfRange").Replace("{max}", maxJobCount.ToString(), StringComparison.Ordinal));
        }

        if (distinctIndexes.Any(index => index > existingJobCount))
        {
            return CliParseResult.Failure(localize("CliJobDoesNotExist"));
        }

        return CliParseResult.Success(distinctIndexes);
    }

    private static CliParseResult ParseRange(string argument, Func<string, string> localize)
    {
        var parts = argument.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !int.TryParse(parts[0], out var start) || !int.TryParse(parts[1], out var end) || start > end)
        {
            return CliParseResult.Failure(localize("CliInvalidRange"));
        }

        return CliParseResult.Success(Enumerable.Range(start, end - start + 1));
    }

    private static CliParseResult ParseList(string argument, Func<string, string> localize)
    {
        var parts = argument.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            return CliParseResult.Failure(localize("CliInvalidList"));
        }

        var indexes = new List<int>();
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var index))
            {
                return CliParseResult.Failure(localize("CliInvalidList"));
            }

            indexes.Add(index);
        }

        return CliParseResult.Success(indexes);
    }

    private static string GetDefaultText(string key)
    {
        return key switch
        {
            "CliArgumentRequired" => "CLI argument is required.",
            "NoJobs" => "No backup jobs are configured.",
            "CliDuplicateIndexes" => "Duplicate job indexes are not allowed.",
            "CliIndexOutOfRange" => "Job indexes must be between 1 and {max}.",
            "CliJobDoesNotExist" => "One or more requested backup jobs do not exist.",
            "CliInvalidRange" => "Invalid range format. Expected example: 1-3.",
            "CliInvalidList" => "Invalid list format. Expected example: 1;3.",
            _ => key
        };
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
