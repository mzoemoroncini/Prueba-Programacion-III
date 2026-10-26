namespace Offers.Api.Features.OfferCapture;

public interface IOfferExtractor
{
    Task<ExtractionResult> ExtractAsync(SourceDocument document, CaptureSettings settings, CancellationToken token);
}
