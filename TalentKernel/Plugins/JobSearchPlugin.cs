using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Net.Http.Json;
using TalentKernel.Models;

namespace TalentKernel.Plugins;

/// <summary>
/// Provides capabilities to search for job vacancies using the Adzuna API.
/// </summary>
public class JobSearchPlugin(string appKey, string appId, IHttpClientFactory httpClientFactory)
{

    private readonly string _appKey = appKey;
    private readonly string _appId = appId;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("AdzunaClient");


    /// <summary>
    /// Searches for job vacancies based on keywords, location, and recency.
    /// </summary>
    /// <param name="keywords">The job titles or skills to search for (e.g., '.NET Developer', 'Go Engineer').</param>
    /// <param name="countryCode">ISO 3166-1 alpha-2 country code (e.g., 'de' for Germany, 'cz' for Czech Republic).</param>
    /// <param name="maxDaysOld">The maximum age of the job advertisement in days.</param>
    /// <param name="resultsPerPage">Maximum number of results to return (default is 5 for token efficiency).</param>
    [KernelFunction]
    [Description("Searches for real-time job openings in specific countries using keywords and date filters.")]
    public async Task<List<JobOpportunity>> SearchJobs(
        [Description("Search terms, e.g., 'Senior .NET Developer'")] string keywords,
        [Description("Country code (ISO 3166-1 alpha-2), e.g., 'de', 'at', 'cz', 'gb'")] string countryCode = "de",
        [Description("Maximum age of the job listing in days")] int maxDaysOld = 30,
        [Description("The minimum salary for the position")] double? salaryMin = null)
    {
        // Base URL construction following Adzuna's documentation
        var endpoint = $"https://api.adzuna.com/v1/api/jobs/{countryCode.ToLower()}/search/1";

        var queryParams = new List<string>
        {
            $"app_id={_appId}",
            $"app_key={_appKey}",
            $"results_per_page=5",
            $"what={Uri.EscapeDataString(keywords)}",
            $"max_days_old={maxDaysOld}",
            "content-type=application/json"
        };

        if (salaryMin.HasValue)
            queryParams.Add($"salary_min={salaryMin.Value}");

        var fullUrl = $"{endpoint}?{string.Join("&", queryParams)}";

        var response = await _httpClient.GetFromJsonAsync<AdzunaResponse>(fullUrl);

        if (response?.Results == null) return new List<JobOpportunity>();

        return response.Results.Select(r => new JobOpportunity
        {
            Id = r.Id,
            Title = r.Title,
            Company = r.Company.DisplayName,
            Location = r.Location.DisplayName,
            DescriptionUrl = r.RedirectUrl,
            CreatedAt = r.Created,
            SalaryMin = r.SalaryMin,
            Category = r.Category.Label
        }).ToList();
    }

    // API Response mapping records
    private record AdzunaResponse(List<AdzunaResult> Results);
    private record AdzunaResult(
        string Id,
        string Title,
        string RedirectUrl,
        string Created,
        AdzunaCompany Company,
        AdzunaLocation Location,
        AdzunaCategory Category,
        [property: System.Text.Json.Serialization.JsonPropertyName("salary_min")] double? SalaryMin
    );

    private record AdzunaCompany(string DisplayName);
    private record AdzunaLocation(string DisplayName);
    private record AdzunaCategory(string Label);
}