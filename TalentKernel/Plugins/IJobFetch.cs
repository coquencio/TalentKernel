using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using TalentKernel.Models;

namespace TalentKernel.Plugins;

/// <summary>
/// Abstraction for fetching job listings from a given source.
/// Implementations should return a list of JobOpportunity using the shared JobOpportunity model.
/// </summary>
public interface IJobFetch
{
    /// <summary>
    /// Search for job opportunities using keyword-driven queries.
    /// </summary>
    Task<List<JobOpportunity>> SearchJobs(
        [Description("Search terms, e.g., 'Senior .NET Developer'")] string keywords,
        [Description("Country code (ISO 3166-1 alpha-2), e.g., 'de', 'es', 'mx'")] string countryCode = "de",
        [Description("Maximum age of the job listing in days")] int maxDaysOld = 30,
        [Description("The minimum salary for the position")] double? salaryMin = null);
}
