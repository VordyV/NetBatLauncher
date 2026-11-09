
using System.Text;

namespace NBL;

public abstract class ILaunchParam
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? NameKey { get; set; }
    public bool FullFormat { get; set; }
    public required string? Description { get; set; }
    public string? DescriptionKey { get; set; }
    public required string? ShortDescription { get; set; }
}

public abstract class LaunchParam<T> : ILaunchParam
{
    public abstract IEnumerable<string> BuildArgs(T param);
}

public class LaunchParamStr : LaunchParam<string>
{
    public override IEnumerable<string> BuildArgs(string param)
    {
        yield return $"+{Id}";
        yield return param;
    }
}


public class LaunchParamBool : LaunchParam<bool>
{
    public override IEnumerable<string> BuildArgs(bool param)
    {
        if (param)
        {
            yield return $"+{Id}";
            yield return "1";
        }
    }
}


public class LaunchParamDict : LaunchParam<string>
{
    public Dictionary<string, string> Dictionary { get; set; } = new();

    public override IEnumerable<string> BuildArgs(string param)
    {
        if (!Dictionary.TryGetValue(param, out string? args))
            yield break;

        foreach (string arg in SplitArguments(args))
            yield return arg;
    }

    private static IEnumerable<string> SplitArguments(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        foreach (string part in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            yield return part;
    }
}

public class LaunchOptionDict : ILaunchParam
{
    public Dictionary<string, string> Dictionary { get; set; } = new();

    public IEnumerable<string> BuildArgs(IEnumerable<string> options)
    {
        foreach (string option in options)
        {
            if (!Dictionary.TryGetValue(option, out string? args))
                continue;

            foreach (string arg in SplitArguments(args))
                yield return arg;
        }
    }

    private static IEnumerable<string> SplitArguments(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        foreach (string part in value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            yield return part;
    }
}

public class LaunchParamCustom : ILaunchParam
{
    public IEnumerable<string> BuildArgs(string value)
    {
        foreach (string arg in SplitArguments(value))
            yield return arg;
    }

    private static IEnumerable<string> SplitArguments(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;

        StringBuilder current = new();
        bool insideQuotes = false;

        foreach (char character in value)
        {
            if (character == '"')
            {
                insideQuotes = !insideQuotes;
                continue;
            }

            if (char.IsWhiteSpace(character) && !insideQuotes)
            {
                if (current.Length > 0)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                continue;
            }

            current.Append(character);
        }

        if (current.Length > 0)
            yield return current.ToString();
    }
}
