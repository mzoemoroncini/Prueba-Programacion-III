using System.Net;
using System.Text.RegularExpressions;

namespace Offers.Api.Features.OfferCapture;

public static class RobotsPolicy
{
    public static async Task EnsureAllowedAsync(HttpClient http, Uri url, CancellationToken token)
    {
        using var response = await http.GetAsync(new Uri(url.GetLeftPart(UriPartial.Authority) + "/robots.txt"), HttpCompletionOption.ResponseHeadersRead, token);
        if (response.StatusCode == HttpStatusCode.NotFound) return;
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("No se pudo confirmar la política de acceso automatizado de la fuente.");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var memory = new MemoryStream();
        var buffer = new byte[4096]; int count;
        while ((count = await stream.ReadAsync(buffer, token)) != 0)
        {
            if (memory.Length + count > 262144) throw new InvalidOperationException("La política robots supera el tamaño permitido.");
            memory.Write(buffer, 0, count);
        }
        if (!Allows(System.Text.Encoding.UTF8.GetString(memory.ToArray()), url.PathAndQuery))
            throw new InvalidOperationException("La fuente impide esta lectura automatizada mediante robots.txt.");
    }
    public static bool Allows(string text, string path)
    {
        var groups = new List<(List<string> Agents, List<(string Path, bool Allow)> Rules)>();
        var agents = new List<string>(); var rules = new List<(string, bool)>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Split('#')[0].Trim(); var colon = line.IndexOf(':');
            if (colon < 0) continue;
            var directive = line[..colon].Trim().ToLowerInvariant(); var value = line[(colon + 1)..].Trim();
            if (directive == "user-agent")
            {
                if (rules.Count > 0) { groups.Add((agents, rules)); agents = []; rules = []; }
                agents.Add(value.ToLowerInvariant());
            }
            else if (directive is "allow" or "disallow" && agents.Count > 0 && value.Length > 0) rules.Add((value, directive == "allow"));
        }
        groups.Add((agents, rules));
        var matching = groups.Where(g => g.Agents.Any(a => a == "*" || "offersmvp".Contains(a, StringComparison.Ordinal))).ToList();
        if (matching.Any(g => g.Agents.Any(a => a != "*" && "offersmvp".Contains(a, StringComparison.Ordinal))))
            matching = matching.Where(g => g.Agents.Any(a => a != "*" && "offersmvp".Contains(a, StringComparison.Ordinal))).ToList();
        var matches = matching.SelectMany(g => g.Rules).Where(rule => Regex.IsMatch(path,
            "^" + Regex.Escape(rule.Path.TrimEnd('$')).Replace("\\*", ".*") + (rule.Path.EndsWith('$') ? "$" : ""), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
            .OrderByDescending(rule => rule.Path.Count(c => c is not ('*' or '$'))).ThenByDescending(rule => rule.Allow).ToList();
        return matches.Count == 0 || matches[0].Allow;
    }
}
