using System.Net.Http.Json;
using System.Text.Json;

namespace Offers.Api.Features.OfferCapture;

public sealed class GeminiOfferExtractor(HttpClient http) : IOfferExtractor
{
    public async Task<ExtractionResult> ExtractAsync(SourceDocument document, CaptureSettings settings, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Model))
            throw new InvalidOperationException("Faltan la clave de Gemini o el modelo.");
        var parts = new List<object> { new { text = JsonSerializer.Serialize(new {
            source = document.Url, text = document.Text, allowedLinks = document.Links,
            attachmentUrls = document.Attachments.Select(x => x.Url), readDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd") }) } };
        if (document.Screenshot is not null) parts.Add(new { inlineData = new {
            mimeType = "image/jpeg", data = Convert.ToBase64String(document.Screenshot) } });
        foreach (var attachment in document.Attachments) parts.Add(new { inlineData = new {
            mimeType = attachment.MimeType, data = Convert.ToBase64String(attachment.Data) } });
        var body = new {
            systemInstruction = new { parts = new[] { new { text = OfferExtractionContract.Instructions } } },
            contents = new[] { new { role = "user", parts } },
            generationConfig = new {
                maxOutputTokens = 12000,
                responseMimeType = "application/json",
                responseJsonSchema = OfferExtractionContract.Schema()
            }
        };
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/models/" + Uri.EscapeDataString(settings.Model) + ":generateContent")
            { Content = JsonContent.Create(body) };
        request.Headers.Add("x-goog-api-key", settings.ApiKey);
        using var response = await http.SendAsync(request, token);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                ? "Se alcanzó el límite de uso de Gemini. Intentá nuevamente más tarde."
                : "Gemini rechazó la extracción (HTTP " + (int)response.StatusCode + ").");
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        if (!result.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() != 1)
            throw new InvalidOperationException("Gemini no devolvió una extracción completa.");
        var candidate = candidates[0];
        if (!candidate.TryGetProperty("finishReason", out var finishReason) || finishReason.GetString() != "STOP"
            || !candidate.TryGetProperty("content", out var content) || !content.TryGetProperty("parts", out var outputParts))
            throw new InvalidOperationException("La respuesta de Gemini quedó incompleta o fue bloqueada.");
        var payload = string.Concat(outputParts.EnumerateArray()
            .Where(x => (!x.TryGetProperty("thought", out var thought) || !thought.GetBoolean()) && x.TryGetProperty("text", out _))
            .Select(x => x.GetProperty("text").GetString()));
        var extraction = JsonSerializer.Deserialize<ExtractionResult>(payload, OfferExtractionContract.JsonOptions);
        if (extraction?.Offers is null)
            throw new InvalidOperationException("Gemini no devolvió una extracción válida.");
        return extraction;
    }
}
