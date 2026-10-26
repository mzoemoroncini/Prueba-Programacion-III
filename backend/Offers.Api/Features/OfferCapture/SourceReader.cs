using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.Playwright;

namespace Offers.Api.Features.OfferCapture;

public sealed class SourceReader(HttpClient http, CaptureSettings settings)
{
    public async Task<SourceDocument> ReadAsync(CaptureSource source, bool render, CancellationToken token)
    {
        var uri = new Uri(source.Url);
        if (!IsOfficial(uri, source.Chain)) throw new InvalidOperationException("La fuente no pertenece a la cadena configurada.");
        await RobotsPolicy.EnsureAllowedAsync(http, uri, token);
        string html;
        byte[]? screenshot = null;
        if (render)
        {
            using var playwright = await Playwright.CreateAsync();
            var proxyServer = settings.ProxyServer ?? Environment.GetEnvironmentVariable("HTTPS_PROXY") ?? Environment.GetEnvironmentVariable("HTTP_PROXY");
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true, ExecutablePath = settings.BrowserExecutablePath, Proxy = string.IsNullOrWhiteSpace(proxyServer) ? null : new Proxy { Server = proxyServer } });
            await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 }, ServiceWorkers = ServiceWorkerPolicy.Block });
            await context.RouteAsync("**/*", async route =>
            {
                try
                {
                    var resource = new Uri(route.Request.Url);
                    if (resource.Scheme != "https" || !resource.IsDefaultPort || !string.IsNullOrEmpty(resource.UserInfo)
                        || !(IsOfficial(resource, source.Chain) || IsApprovedAssetHost(resource.Host) || await IsPublicHostAsync(resource.Host, token))
                        || (route.Request.Method != "GET" && route.Request.Method != "HEAD"))
                    { await route.AbortAsync(); return; }
                    using var request = new HttpRequestMessage(new HttpMethod(route.Request.Method), resource);
                    using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
                    await using var stream = await response.Content.ReadAsStreamAsync(token);
                    using var memory = new MemoryStream();
                    var buffer = new byte[8192];
                    int count;
                    while ((count = await stream.ReadAsync(buffer, token)) != 0)
                    {
                        if (memory.Length + count > 8_000_000) throw new InvalidOperationException("Recurso de navegador demasiado grande.");
                        memory.Write(buffer, 0, count);
                    }
                    var headers = response.Headers.Concat(response.Content.Headers)
                        .Where(x => !new[] { "content-encoding", "content-length", "transfer-encoding" }.Contains(x.Key.ToLowerInvariant()))
                        .ToDictionary(x => x.Key, x => string.Join(",", x.Value));
                    await route.FulfillAsync(new() { Status = (int)response.StatusCode, Headers = headers, BodyBytes = memory.ToArray() });
                }
                catch (Exception) { await route.AbortAsync(); }
            });
            var page = await context.NewPageAsync();
            var response = await page.GotoAsync(uri.AbsoluteUri, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 45000 });
            token.ThrowIfCancellationRequested();
            if (response is null || !response.Ok || !IsOfficial(new Uri(page.Url), source.Chain))
                throw new InvalidOperationException("La página no pudo leerse o redirigió fuera de la fuente oficial.");
            try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 10000 }); }
            catch (System.TimeoutException) { }
            catch (PlaywrightException error) when (error.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase)) { }
            html = await page.ContentAsync();
            screenshot = await page.ScreenshotAsync(new() { Type = ScreenshotType.Jpeg, Quality = 75, FullPage = true });
            if (screenshot.Length > 8_000_000) throw new InvalidOperationException("La imagen de la página supera el límite de lectura.");
        }
        else html = System.Text.Encoding.UTF8.GetString(await DownloadAsync(uri, 2_000_000, token));
        if (html.Length > 2_000_000) throw new InvalidOperationException("La página supera el límite de lectura.");
        var doc = new HtmlDocument(); doc.LoadHtml(html);
        var links = (doc.DocumentNode.SelectNodes("//a[@href]") ?? new HtmlNodeCollection(null))
            .Select(x => Uri.TryCreate(uri, x.GetAttributeValue("href", ""), out var link) ? link : null)
            .Where(x => x is not null && IsOfficial(x, source.Chain)).Select(x => x!.AbsoluteUri).Distinct().ToList();
        links.Add(uri.AbsoluteUri);
        var attachments = new List<SourceAttachment>();
        foreach (var link in links.Where(x => new Uri(x).AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).Take(3))
        {
            await RobotsPolicy.EnsureAllowedAsync(http, new Uri(link), token);
            var bytes = await DownloadAsync(new Uri(link), 8_000_000, token);
            if (bytes.Length < 5 || System.Text.Encoding.ASCII.GetString(bytes, 0, 5) != "%PDF-")
                throw new InvalidOperationException("Un documento enlazado no contiene un PDF válido.");
            attachments.Add(new SourceAttachment(link, Path.GetFileName(new Uri(link).AbsolutePath), "application/pdf", bytes));
        }
        foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//noscript") ?? new HtmlNodeCollection(null)) node.Remove();
        var text = WebUtility.HtmlDecode(doc.DocumentNode.InnerText);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        if (text.Length > 100000) throw new InvalidOperationException("El texto supera el límite de extracción; requiere una fuente más específica.");
        if (text.Length < 30 && screenshot is null) throw new InvalidOperationException("La fuente no contiene información legible.");
        return new SourceDocument(uri.AbsoluteUri, text, screenshot, attachments, links);
    }

    private async Task<byte[]> DownloadAsync(Uri url, int maxBytes, CancellationToken token)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.RequestMessage?.RequestUri?.Host != url.Host) throw new InvalidOperationException("El recurso redirigió a otro dominio.");
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(buffer, token)) != 0)
        {
            if (memory.Length + count > maxBytes) throw new InvalidOperationException("El recurso supera el límite de lectura.");
            memory.Write(buffer, 0, count);
        }
        return memory.ToArray();
    }

    private static bool IsApprovedAssetHost(string host) =>
        host.EndsWith(".appmcdonalds.com", StringComparison.Ordinal) ||
        host.EndsWith(".rbi.com", StringComparison.Ordinal) ||
        host is "fonts.googleapis.com" or "fonts.gstatic.com" or "ajax.googleapis.com";

    private static async Task<bool> IsPublicHostAsync(string host, CancellationToken token)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, token);
        return addresses.Length > 0 && addresses.All(address =>
        {
            if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return false;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            var bytes = address.GetAddressBytes();
            if (bytes.Length == 16) return !address.Equals(IPAddress.IPv6Any) && (bytes[0] & 0xfe) != 0xfc;
            return bytes[0] != 0 && bytes[0] != 10 && bytes[0] != 127 && bytes[0] < 224
                && !(bytes[0] == 169 && bytes[1] == 254) && !(bytes[0] == 192 && bytes[1] == 168)
                && !(bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                && !(bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127);
        });
    }

    public static bool IsOfficial(Uri uri, Offers.Api.Features.OfferPublication.RestaurantChain chain)
    {
        var domain = chain switch {
            Offers.Api.Features.OfferPublication.RestaurantChain.McDonalds => "mcdonalds.com.ar",
            Offers.Api.Features.OfferPublication.RestaurantChain.BurgerKing => "burgerking.com.ar",
            Offers.Api.Features.OfferPublication.RestaurantChain.Mostaza => "mostazaweb.com.ar", _ => "" };
        return uri.Scheme == "https" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo) && domain.Length > 0 &&
            (uri.Host == domain || uri.Host.EndsWith("." + domain, StringComparison.Ordinal));
    }
}
