using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Offers.Api.Features.OfferCapture;

public sealed class OpenAiOfferExtractor(HttpClient http) : IOfferExtractor
{
    public async Task<ExtractionResult> ExtractAsync(SourceDocument document, CaptureSettings settings, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Model))
            throw new InvalidOperationException("Faltan la clave de IA o el modelo.");
        var content = new List<object> { new { type = "input_text", text = JsonSerializer.Serialize(new {
            source = document.Url, text = document.Text, allowedLinks = document.Links,
            attachmentUrls = document.Attachments.Select(x => x.Url), readDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd") }) } };
        if (document.Screenshot is not null) content.Add(new { type = "input_image", image_url = "data:image/jpeg;base64," + Convert.ToBase64String(document.Screenshot), detail = "high" });
        foreach (var attachment in document.Attachments) content.Add(new {
            type = "input_file", filename = attachment.Filename, file_data = "data:" + attachment.MimeType + ";base64," + Convert.ToBase64String(attachment.Data) });
        var body = new {
            model = settings.Model, store = false, max_output_tokens = 12000,
            instructions = OfferExtractionContract.Instructions,
            input = new[] { new { role = "user", content } },
            text = new { format = new { type = "json_schema", name = "offers", strict = true, schema = OfferExtractionContract.Schema() } }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses") { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        using var response = await http.SendAsync(request, token);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException("El proveedor de IA rechazó la extracción (HTTP " + (int)response.StatusCode + ").");
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        if (!result.RootElement.TryGetProperty("status", out var status) || status.GetString() != "completed")
            throw new InvalidOperationException("La respuesta de IA no se completó.");
        var texts = result.RootElement.GetProperty("output").EnumerateArray()
            .Where(x => x.TryGetProperty("type", out var type) && type.GetString() == "message")
            .SelectMany(x => x.GetProperty("content").EnumerateArray())
            .Where(x => x.GetProperty("type").GetString() == "output_text").Select(x => x.GetProperty("text").GetString());
        var payload = string.Concat(texts);
        return JsonSerializer.Deserialize<ExtractionResult>(payload, OfferExtractionContract.JsonOptions)
            ?? throw new InvalidOperationException("La IA no devolvió una extracción válida.");
    }

}
