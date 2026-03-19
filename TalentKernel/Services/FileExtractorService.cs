using UglyToad.PdfPig;

namespace TalentKernel.Services;
public class FileExtractorService
{
    private readonly HttpClient _httpClient;

    public FileExtractorService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    public async Task<string> ExtractTextFromPdf(string url)
    {
        var response = await _httpClient.GetAsync(url);
        if (!response.Content.Headers.ContentType?.MediaType?.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) ?? true)
        {
            return "The downloaded file is not a PDF.";
        }
        using var stream = await response.Content.ReadAsStreamAsync();
        using var pdf = PdfDocument.Open(stream);

        var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
        return text.Length > 8000 ? text[..8000] : text; // Truncate to avoid token limits
    }
}

