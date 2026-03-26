using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Threading.Tasks;
using TalentKernel.Models;
using TalentKernel.Services;

namespace TalentKernel.Plugins;

public class JobSearchPlugin
{
    private readonly string _appKey;
    private readonly string _appId;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IJobFetchFactory _jobFetchFactory;

    public JobSearchPlugin(string appKey, string appId, IHttpClientFactory httpClientFactory, IJobFetchFactory jobFetchFactory)
    {
        _appKey = appKey;
        _appId = appId;
        _httpClientFactory = httpClientFactory;
        _jobFetchFactory = jobFetchFactory ?? throw new ArgumentNullException(nameof(jobFetchFactory));
    }

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
        // Use factory to obtain a provider-specific IJobFetch implementation and delegate
        var fetcher = _jobFetchFactory.Create("adzuna", _appKey, _appId, _httpClientFactory);
        return await fetcher.SearchJobs(keywords, countryCode, maxDaysOld, salaryMin);
    }
}