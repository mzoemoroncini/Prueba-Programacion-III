using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Offers.Api.Features.OfferCapture;

public sealed class OcrSettings
{
    public bool Enabled { get; set; }
    public string? ApiKey { get; set; }
    public bool IsReady => !Enabled || !string.IsNullOrWhiteSpace(ApiKey);
}

public sealed class OcrSpaceClient(HttpClient http)
{
    public const int MaxFileBytes = 1_000_000;
    public async Task<SourceDocument> ReadAsync(SourceDocument document, OcrSettings settings, CancellationToken token)
    {
        if (!settings.Enabled) return document;
        if (!settings.IsReady) throw new InvalidOperationException("Falta configurar la clave de OCR.space.");
        var files = new List<SourceAttachment>();
        if (document.Screenshot is not null) files.Add(new(document.Url, "page.jpg", "image/jpeg", document.Screenshot));
        files.AddRange(document.Attachments);
        foreach (var file in files)
        {
            if (file.Data.Length == 0 || file.Data.Length > MaxFileBytes)
                throw new InvalidOperationException("OCR.space gratuito admite archivos de hasta 1 MB. No se truncará el archivo.");
            if (file.MimeType is not ("image/jpeg" or "image/png" or "application/pdf"))
                throw new InvalidOperationException("El tipo de archivo no está admitido por esta integración.");
        }
        var text = new StringBuilder(document.Text);
        foreach (var file in files)
        {
            using var form = new MultipartFormDataContent();
            var bytes = new ByteArrayContent(file.Data);
            bytes.Headers.ContentType = new MediaTypeHeaderValue(file.MimeType);
            form.Add(bytes, "file", file.Filename);
            form.Add(new StringContent("2"), "OCREngine");
            form.Add(new StringContent("auto"), "language");
            form.Add(new StringContent("false"), "isOverlayRequired");
            form.Add(new StringContent("false"), "isCreateSearchablePdf");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ocr.space/parse/image") { Content = form };
            request.Headers.Add("apikey", settings.ApiKey);
            using var response = await http.SendAsync(request, token);
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("OCR.space rechazó la lectura (HTTP " + (int)response.StatusCode + ").");
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var root = result.RootElement;
            if (root.GetProperty("IsErroredOnProcessing").GetBoolean() || ExitCode(root.GetProperty("OCRExitCode")) != 1)
                throw new InvalidOperationException("OCR.space no pudo leer el archivo completo.");
            var pages = root.GetProperty("ParsedResults").EnumerateArray().ToList();
            if (pages.Count == 0 || (file.MimeType == "application/pdf" && pages.Count > 3)
                || pages.Any(x => ExitCode(x.GetProperty("FileParseExitCode")) != 1))
                throw new InvalidOperationException("OCR.space devolvió páginas incompletas o fuera del límite.");
            text.AppendLine().AppendLine("[Texto OCR de " + file.Url + "]");
            foreach (var page in pages)
            {
                var parsed = page.GetProperty("ParsedText").GetString();
                if (parsed is null) throw new InvalidOperationException("OCR.space no devolvió el texto de una página.");
                text.AppendLine(parsed);
            }
        }
        if (text.Length > 150_000) throw new InvalidOperationException("El texto OCR supera el límite de extracción.");
        return document with { Text = text.ToString(), Screenshot = null, Attachments = [] };
    }
    private static int ExitCode(JsonElement value) => value.ValueKind == JsonValueKind.Number
        ? value.GetInt32() : int.Parse(value.GetString()!, CultureInfo.InvariantCulture);
}
