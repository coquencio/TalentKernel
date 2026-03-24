using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TalentKernel.Models;

namespace TalentKernel.Plugins;

public class JobSearchPlugin(string appKey, string appId, IHttpClientFactory httpClientFactory)
{
    private readonly string _appKey = appKey;
    private readonly string _appId = appId;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("AdzunaClient");

    [KernelFunction]
    [Description("""
    Searches for real-time job openings using keyword-driven queries against an external jobs API.

    Usage guidance and intent:
    - This plugin is the primary keyword-based job search tool and should be used when the user requests
      a job search but does not provide a CV or profile information. If the user supplies a CV/profile,
      prefer the CvOrchestratorPlugin to drive a CV-first workflow instead.
    - Use concise, role-and-skill-focused keywords (for example: "Senior .NET Backend Developer", "DevOps engineer",
      "Frontend React developer") for the keywords parameter. Do not use this parameter to pass semantic
      constraints like "visa sponsorship" or "relocation support"—those should be handled by higher-level
      analyst/orchestrator workflows if needed.

    Examples the LLM can follow when deciding to call this plugin:
    - "Find remote .NET developer jobs in Germany" -> keywords = "remote .NET developer", countryCode = "de".
    - "Show me junior frontend roles in Spain" -> keywords = "junior frontend developer", countryCode = "es".
    - "I don't have a resume, just look for cloud engineer roles in Mexico" -> use this plugin as the fallback.

    Caller expectations and returned data:
    - Returns a list of JobOpportunity objects with Id, Title, Company, Location, DescriptionUrl (apply URL),
      CreatedAt, SalaryMin and Category. Callers should post-process or enrich results (for example via the
      MarkdownReaderPlugin and JobAnalystPlugin) when deeper semantic checks are required (visa, relocation,
      required languages, years of experience).

    Notes for integration:
    - If follow-up semantic checks are needed (e.g., whether a job offers visa sponsorship), call the
      MarkdownReaderPlugin to fetch the job page and then the JobAnalystPlugin to perform criteria analysis.
    - Keep keyword queries focused and avoid embedding user-specific profile facts into the keywords; instead,
      prefer the orchestrator when profile information is available.
    """)]
    public async Task<List<JobOpportunity>> SearchJobs(
        [Description("Search terms, e.g., 'Senior .NET Developer'")] string keywords,
        [Description("Country code (ISO 3166-1 alpha-2), e.g., 'de', 'es', 'mx'")] string countryCode = "de",
        [Description("Maximum age of the job listing in days")] int maxDaysOld = 30,
        [Description("The minimum salary for the position")] double? salaryMin = null)
    {
        var endpoint = $"https://api.adzuna.com/v1/api/jobs/{countryCode.ToLower()}/search/1";

        var queryParams = new List<string>
        {
            $"app_id={_appId}",
            $"app_key={_appKey}",
            $"results_per_page=5",
            $"what={Uri.EscapeDataString(keywords)}",
            $"max_days_old={maxDaysOld}"
        };

        if (salaryMin.HasValue)
            queryParams.Add($"salary_min={salaryMin.Value}");

        var fullUrl = $"{endpoint}?{string.Join("&", queryParams)}";

        var responseMessage = await _httpClient.GetAsync(fullUrl);
        responseMessage.EnsureSuccessStatusCode();

        // LEER COMO BYTES: Esto ignora el charset corrupto del header 'Content-Type'
        var bytes = await responseMessage.Content.ReadAsByteArrayAsync();

        // FORZAR UTF-8: Aquí nosotros mandamos, no el header de Adzuna
        var jsonContent = Encoding.UTF8.GetString(bytes);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        var response = JsonSerializer.Deserialize<AdzunaResponse>(jsonContent, options);

        if (response?.Results == null) return new List<JobOpportunity>();

        return response.Results.Select(r => new JobOpportunity
        {
            Id = r.Id,
            Title = r.Title,
            Company = r.Company?.DisplayName ?? "Unknown",
            Location = r.Location?.DisplayName ?? "Remote/Unknown",
            DescriptionUrl = r.RedirectUrl,
            CreatedAt = r.Created,
            SalaryMin = r.SalaryMin,
            Category = r.Category?.Label ?? "General"
        }).ToList();
    }

    private record AdzunaResponse([property: JsonPropertyName("results")] List<AdzunaResult> Results);

    private record AdzunaResult(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("redirect_url")] string RedirectUrl,
        [property: JsonPropertyName("created")] string Created,
        [property: JsonPropertyName("company")] AdzunaCompany Company,
        [property: JsonPropertyName("location")] AdzunaLocation Location,
        [property: JsonPropertyName("category")] AdzunaCategory Category,
        [property: JsonPropertyName("salary_min")] double? SalaryMin
    );

    private record AdzunaCompany([property: JsonPropertyName("display_name")] string DisplayName);
    private record AdzunaLocation([property: JsonPropertyName("display_name")] string DisplayName);
    private record AdzunaCategory([property: JsonPropertyName("label")] string Label);
}