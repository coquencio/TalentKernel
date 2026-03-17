using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net;
namespace TalentKernel.Plugins;
public class MarkdownBatchReaderPlugin(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("JinaReaderClient");
    public record JobContent(string Id, string Markdown);

    /// <summary>
    /// Converts a list of job URLs to Markdown in parallel.
    /// </summary>
    /// <param name="urls">List of job posting URLs.</param>
    /// <returns>A list of JobContent objects containing the cleaned text.</returns>
    [KernelFunction]
    [Description("Converts multiple job URLs into clean Markdown content simultaneously.")]
    public async Task<List<JobContent>> ReadJobsInBatch(List<string> urls)
    {
        // We trigger all HTTP requests in parallel
        var tasks = urls.Select(async url =>
        {
            try
            {
                // Jina Reader is perfect for this. We prepend the URL.
                var readerUrl = $"https://r.jina.ai/{url}";
                var content = await _httpClient.GetStringAsync(readerUrl);

                // Smart Truncating: Keep only the most relevant part (first 4000 chars)
                // to avoid blowing up the LLM context window/costs.
                var cleanContent = content.Length > 4000
                    ? content.Substring(0, 4000)
                    : content;

                return new JobContent(Id: url, Markdown: cleanContent);
            }
            catch (Exception ex)
            {
                // If one fails, we return a small note so the LLM knows it couldn't be read
                return new JobContent(Id: url, Markdown: $"Error reading content: {ex.Message}");
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }


    /// <summary>
    /// Reads a single job URL and returns its content in Markdown.
    /// </summary>
    [KernelFunction]
    [Description("Reads the content of a specific job posting URL and returns it as clean Markdown. Use this when the user provides a direct link.")]
    public async Task<JobContent> ReadSingleJob(
        [Description("The full URL of the job posting")] string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uriResult) ||
            (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
        {
            return new JobContent(url, "Error: The URL provided is invalid or uses an insecure protocol.");
        }
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var readerUrl = $"https://r.jina.ai/{url}";
            var response = await _httpClient.GetAsync(readerUrl, cts.Token);

            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound)
            {
                return new JobContent(url, $"Error: The website at {url} blocked the reader or the page doesn't exist.");
            }

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cts.Token);

            var cleanContent = content.Length > 6000 ? content.Substring(0, 6000) : content;

            return new JobContent(url, cleanContent);
        }
        catch (OperationCanceledException)
        {
            return new JobContent(url, "Error: The request timed out. The site took too long to respond.");
        }
        catch (Exception ex)
        {
            return new JobContent(url, $"Error: Could not process the URL. Technical details: {ex.Message}");
        }
    }
}
