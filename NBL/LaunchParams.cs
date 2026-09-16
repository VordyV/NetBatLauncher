using System.Text;
namespace NBL;
public abstract class ILaunchParam
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public bool FullFormat { get; set; } = false;
}
public abstract class LaunchParam<T> : ILaunchParam
{
    public abstract IEnumerable<string> BuildArgs(T param);
}
public class LaunchParamStr : LaunchParam<string>
{
    public override IEnumerable<string> BuildArgs(
        string param)
    {
        yield return $"+{this.Id}";
        yield return param;
    }
}
public class LaunchParamBool : LaunchParam<bool>
{
    public override IEnumerable<string> BuildArgs(
        bool param)
    {
        yield return $"+{this.Id}";
        yield return param ? "1" : "0";
    }
}
public class LaunchParamDict : LaunchParam<string>
{
    public Dictionary<string, string> Dictionary { get; set; } =
        new();
    public override IEnumerable<string> BuildArgs(
        string param)
    {
        if (!this.Dictionary.TryGetValue(
                param,
                out string? args))
        {
            yield break;
        }
        foreach (string arg in SplitArguments(args))
        {
            yield return arg;
        }
    }
    private static IEnumerable<string> SplitArguments(
        string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            yield break;
        string[] parts =
            value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);
        foreach (string part in parts)
        {
            yield return part;
        }
    }
    public class LaunchOptionDict : ILaunchParam
    {
        public Dictionary<string, string> Dictionary { get; set; } =
            new();
        public IEnumerable<string> BuildArgs(
            IEnumerable<string> options)
        {
            foreach (string option in options)
            {
                if (!Dictionary.TryGetValue(
                        option,
                        out string? args))
                {
                    continue;
                }
                foreach (string arg in SplitArguments(args))
                {
                    yield return arg;
                }
            }
        }
        private static IEnumerable<string> SplitArguments(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                yield break;
            foreach (string part in value.Split(
                         ' ',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                yield return part;
            }
        }
    }
    public class LaunchParamCustom : ILaunchParam
    {
        public IEnumerable<string> BuildArgs(string value)
        {
            foreach (string arg in SplitArguments(value))
            {
                yield return arg;
            }
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
            {
                yield return current.ToString();
            }
        }
    }
}