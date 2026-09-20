namespace Host.Bootstrap;

internal static class CommandOptions
{
    /// <summary>`--key value` and `--key=value`.</summary>
    public static Dictionary<string, string> Parse(string[] args)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
                continue;

            var key = args[i][2..];
            var equals = key.IndexOf('=');
            if (equals >= 0)
                parsed[key[..equals]] = key[(equals + 1)..];
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                parsed[key] = args[++i];
        }

        return parsed;
    }
}
