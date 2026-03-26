using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TalentKernel.Models;
using TalentKernel.Plugins;

namespace TalentKernel.Services;

/// <summary>
/// Adzuna implementation of IJobFetch. Contains all API-specific settings and logic.
/// </summary>
public class AdzunaJobFetch : IJobFetch
{
    private readonly string _appKey;
    private readonly string _appId;
    private readonly HttpClient _httpClient;

    public AdzunaJobFetch(string appKey, string appId, IHttpClientFactory httpClientFactory)
    {
        _appKey = appKey ?? string.Empty;
        _appId = appId ?? string.Empty;
        if (httpClientFactory is null) throw new ArgumentNullException(nameof(httpClientFactory));
        _httpClient = httpClientFactory.CreateClient("AdzunaClient");
    }

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

        // Read as bytes and force UTF8 because some providers return bad charset headers
        var bytes = await responseMessage.Content.ReadAsByteArrayAsync();
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
