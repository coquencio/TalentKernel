using Microsoft.SemanticKernel;
using System.ComponentModel;
using TalentKernel.Models;
using TalentKernel.Services;

namespace TalentKernel.Plugins;
public class CvOrchestratorPlugin
{
    private readonly FileExtractorService _fileExtractor;
    private readonly ProfilerService _profiler;
    private readonly JobSearchPlugin _jobSearch;
    private readonly MarkdownReaderPlugin _markdownReader;
    private readonly JobAnalystPlugin _analyst;

    public CvOrchestratorPlugin(
        FileExtractorService fileExtractor,
        ProfilerService profiler,
        JobSearchPlugin jobSearch,
        MarkdownReaderPlugin markdownReader,
        JobAnalystPlugin analyst)
    {
        _fileExtractor = fileExtractor;
        _profiler = profiler;
        _jobSearch = jobSearch;
        _markdownReader = markdownReader;
        _analyst = analyst;
    }

    public class OrchestratorResult
    {
        public bool Found { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<JobOpportunity> Jobs { get; set; } = new();
    }

    /// <summary>
    /// Orchestrates the full flow: extract text from a CV PDF, profile it, search jobs, and filter them with the batch reader + analyst.
    /// </summary>
    [KernelFunction]
    [Description("Given either a PDF URL (CV) or raw CV text, a country code and additional criteria, return matching job offers with apply URLs.")]
    public async Task<OrchestratorResult> OrchestrateCvJobSearch(
        Kernel kernel,
        [Description("Publicly accessible URL to the candidate CV in PDF format (optional if rawCvText is provided)")] string? pdfUrl = null,
        [Description("Raw CV text pasted directly (optional if pdfUrl is provided)")] string? rawCvText = null,
        [Description("Country code for job search (ISO alpha-2)")] string countryCode = "de",
        [Description("Additional criteria to filter jobs, e.g. 'Remote', 'Relocation', 'German language'")] string[]? additionalCriteria = null)
    {
        // 1. Determine source of CV text: raw text takes precedence over PDF URL
        string extracted;
        if (!string.IsNullOrWhiteSpace(rawCvText))
        {
            extracted = rawCvText!;
        }
        else if (!string.IsNullOrWhiteSpace(pdfUrl))
        {
            // Extract text from the PDF
            extracted = await _fileExtractor.ExtractTextFromPdf(pdfUrl!);
            if (string.IsNullOrWhiteSpace(extracted) || extracted.StartsWith("The downloaded file is not a PDF."))
            {
                return new OrchestratorResult { Found = false, Message = "Could not extract a valid PDF from the provided URL." };
            }
        }
        else
        {
            return new OrchestratorResult { Found = false, Message = "No CV provided. Please supply either a PDF URL or paste the CV text." };
        }

        // 2. Build a profile from the resume
        var profile = await _profiler.ParseResume(kernel, extracted);

        // 3. Build keywords from the profile
        var keywordsParts = new List<string>();
        if (profile.CoreSkills?.Any() ?? false) keywordsParts.AddRange(profile.CoreSkills);
        if (profile.PreferredRoles?.Any() ?? false) keywordsParts.AddRange(profile.PreferredRoles);
        if (string.IsNullOrWhiteSpace(profile.Summary) && !keywordsParts.Any())
        {
            // fallback
            keywordsParts.Add("developer");
        }

        var keywords = string.Join(" ", keywordsParts);

        // 4. Search jobs using the JobSearch plugin
        var rawJobs = await _jobSearch.SearchJobs(keywords, countryCode);
        if (rawJobs == null || !rawJobs.Any())
        {
            return new OrchestratorResult { Found = false, Message = "No jobs found for the extracted profile and criteria." };
        }

        // 5. Convert job URLs to markdown content in batch
        var urls = rawJobs.Select(j => j.DescriptionUrl).Where(u => !string.IsNullOrWhiteSpace(u)).ToList();
        var markdowns = await _markdownReader.ReadJobsInBatch(urls);

        // 6. Map markdown reader results to the top-level JobContent type used by the analyst
        var analystJobs = markdowns.Select(m => new JobContent(m.Id, m.Markdown)).ToList();

        // 7. Analyze jobs against additional criteria
        var analysis = await _analyst.AnalyzeJobsBatch(kernel, analystJobs, additionalCriteria ?? Array.Empty<string>());

        // 8. Map analysis results back to original job objects
        var matched = new List<JobOpportunity>();

        foreach (var a in analysis)
        {
            // Try to retrieve JobId from found details, otherwise skip
            string? jobId = null;
            if (a.FoundDetails != null && a.FoundDetails.Count > 0)
            {
                if (a.FoundDetails.TryGetValue("JobId", out var v)) jobId = v;
                else if (a.FoundDetails.TryGetValue("Id", out var v2)) jobId = v2;
            }

            JobOpportunity? match = null;
            if (!string.IsNullOrWhiteSpace(jobId))
            {
                match = rawJobs.FirstOrDefault(j => j.Id == jobId || j.DescriptionUrl == jobId || j.DescriptionUrl == jobId.Replace("https://", ""));
            }

            // If we couldn't match by id, try to match by URL using the analyst JobContent list order (best-effort)
            if (match == null && analystJobs.Count > 0)
            {
                // try to find by markdown id in original rawJobs
                var jobContent = analystJobs.FirstOrDefault(x => x.Id == jobId);
                if (jobContent != null)
                {
                    match = rawJobs.FirstOrDefault(j => j.DescriptionUrl == jobContent.Id || j.Id == jobContent.Id);
                }
            }

            if (match != null)
            {
                matched.Add(match);
            }
        }

        if (!matched.Any())
        {
            return new OrchestratorResult { Found = false, Message = "No jobs matched the additional criteria provided." };
        }

        return new OrchestratorResult { Found = true, Message = "Jobs found.", Jobs = matched };
    }
}
