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
    [Description("Searches for real-time job openings in specific countries using keywords and date filters.")]
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