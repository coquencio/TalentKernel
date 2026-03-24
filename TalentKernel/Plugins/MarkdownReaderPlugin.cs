using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net;
using System.Net.Http.Headers;

namespace TalentKernel.Plugins;

public class MarkdownReaderPlugin(IHttpClientFactory httpClientFactory)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("JinaReaderClient");
    public record JobContent(string Id, string Markdown);

    /// <summary>
    /// Reads a single URL and returns its content in Markdown.
    /// Optimized to bypass Privacy Policies and Cookie Banners.
    /// </summary>
    [KernelFunction]
    [Description("Reads any URL. Use this when the user provides a link and only wants details or a concise summary about the provided page.")]
    public async Task<JobContent> ReadUrlAsMarkdown(
        [Description("The full absolute URL to read (e.g., 'https://example.com/job-post')")] string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uriResult) ||
            (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
        {
            return new JobContent(url, "Error: Invalid URL or insecure protocol.");
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

            // Jina Reader Setup with specialized Headers to bypass common web hurdles
            var request = new HttpRequestMessage(HttpMethod.Get, $"https://r.jina.ai/{url}");
            request.Headers.Add("X-No-Cookie", "true"); // Attempt to bypass cookie consent walls
            request.Headers.Add("X-Return-Format", "markdown");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

            var response = await _httpClient.SendAsync(request, cts.Token);

            if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound)
            {
                return new JobContent(url, $"Error: Access blocked or page not found at {url}.");
            }

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync(cts.Token);

            // --- NOISE REDUCTION LOGIC ---
            // Many recruitment platforms (Teamtailor, Lever) put massive Privacy Policies at the top.
            // We search for common job post markers to "skip" the legal fluff.
            if (content.Contains("Privacy Policy", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Cookie Policy", StringComparison.OrdinalIgnoreCase))
            {
                // Look for the first Level 1 or Level 2 header, usually where the Job Title/Role starts
                int jobHeaderIndex = content.IndexOf("# ", StringComparison.Ordinal);
                if (jobHeaderIndex == -1) jobHeaderIndex = content.IndexOf("## ", StringComparison.Ordinal);

                // If we found a header within the first 4000 chars, start from there
                if (jobHeaderIndex != -1 && jobHeaderIndex < 4000)
                {
                    content = content.Substring(jobHeaderIndex);
                }
            }

            var cleanContent = content.Length > 8000 ? content.Substring(0, 8000) : content;

            return new JobContent(url, cleanContent);
        }
        catch (OperationCanceledException)
        {
            return new JobContent(url, "Error: Request timed out. The website is too slow.");
        }
        catch (Exception ex)
        {
            return new JobContent(url, $"Error: Could not process the URL. Details: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes multiple URLs in parallel.
    /// </summary>
    public async Task<List<JobContent>> ReadJobsInBatch(List<string> urls)
    {
        var tasks = urls.Select(url => ReadUrlAsMarkdown(url));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }
}