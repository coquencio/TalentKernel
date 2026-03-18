using UglyToad.PdfPig;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace TalentKernel.Plugins;
public class FileExtractorPlugin
{
    private readonly HttpClient _httpClient;

    public FileExtractorPlugin(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient();
    }

    [KernelFunction, Description("Extracts text from a PDF file given its URL.")]
    public async Task<string> ExtractTextFromPdf(string url)
    {
        var response = await _httpClient.GetAsync(url);
        using var stream = await response.Content.ReadAsStreamAsync();
        using var pdf = PdfDocument.Open(stream);

        var text = string.Join("\n", pdf.GetPages().Select(p => p.Text));
        return text.Length > 8000 ? text[..8000] : text; // Truncate to avoid token limits
    }
}

