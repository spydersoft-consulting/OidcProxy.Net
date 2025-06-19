using System.Text.RegularExpressions;

namespace OidcProxy.Net.ModuleInitializers.Configuration;

public class SkipAuthRoutes : ISkipAuthRoutes, IApiRoutes
{
    private readonly List<(string? Method, Regex PathRegex, bool Negate)> rules = new();

    public SkipAuthRoutes(IEnumerable<string> bypassAuthConfiguration)
    {
        ParseConfig(bypassAuthConfiguration.SelectMany(c => c.Split(",")));
    }

    private void ParseConfig(IEnumerable<string> bypassAuthConfiguration)
    {
        foreach (var line in bypassAuthConfiguration)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var match = Regex.Match(trimmed, "^(?:(\\w+)(!?))?=?(.+)$");
            if (!match.Success)
            {
                throw new ArgumentException($"Invalid config format: {trimmed}");
            }

            var method = match.Groups[1].Success ? match.Groups[1].Value : null;
            var negate = match.Groups[2].Success && match.Groups[2].Value == "!";
            var pathPattern = match.Groups[3].Value;
            var pathRegex = new Regex(pathPattern);

            rules.Add((method, pathRegex, negate));
        }
    }

    public bool ShouldBypass(string method, string path)
    {
        foreach (var (ruleMethod, pathRegex, negate) in rules)
        {
            bool methodMatches = ruleMethod == null || ruleMethod.Equals(method, StringComparison.OrdinalIgnoreCase);
            bool pathMatches = pathRegex.IsMatch(path);

            if (methodMatches && (negate ? !pathMatches : pathMatches))
            {
                return true;
            }
        }
        return false;
    }

    public bool Matches(string method, string path)
    {
        return ShouldBypass(method, path);
    }
}