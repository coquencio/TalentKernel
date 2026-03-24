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
    [Description("""
    Primary orchestrator that drives the CV-first job discovery workflow. This is the preferred plugin to call
    whenever a user provides CV/profile information (raw text or a PDF attachment) and requests job matching.

    Key behavior and priorities:
    - This plugin is the MAIN tool for flows that include a user's CV/profile. If the user supplies CV text or
      attaches a resume PDF, prefer this orchestrator rather than calling JobSearchPlugin directly.
    - rawCvText takes precedence. If rawCvText is not provided and pdfUrl is present, the plugin will attempt
      to extract plain text from the provided PDF attachment URL.
    - Treat any PDF URL as an attachment URL (for example, a Discord attachment URL). Callers should ensure the
      URL is publicly accessible or reachable by the extraction service.
    - When a CV is successfully extracted as plain text, persist the CV/profile to a user-scoped memory store
      so it can be reused by future operations (for example, to generate cover letters). Confirm storage to the
      user unless they opt out.
    - If insufficient profile details are present, the orchestrator should prefer to ask the user clarifying
      questions (e.g., preferred roles, seniority, remote vs on-site, visa needs) before returning final results.

    AdditionalCriteria semantics:
    - additionalCriteria is NOT intended for canonical role/skill keywords (e.g., "software developer", ".NET",
      "cloud"). Those are derived from the CV/profile. Instead, additionalCriteria is intended for semantic
      job attributes and constraints that affect match suitability, for example:
        "relocation support", "visa sponsorship", "hybrid model", "fully remote", "no English required",
        "willing to consider candidates without college degree", "junior-friendly", "sponsorship for internships",
        "relocation to Germany", "works-with-timezones CET", "on-site in Berlin",
      Use these values to semantically filter and rank jobs.

    Workflow summary:
    1) Extract and/or accept CV text.
    2) Profile the candidate to build keywords, skills, preferred roles, summary and constraints.
    3) Use the JobSearchPlugin with profile-derived keywords to fetch initial job results.
    4) Use the MarkdownReaderPlugin and JobAnalystPlugin to read and semantically analyze job content.
    5) Filter and rank jobs based on additionalCriteria and profile fit, returning job opportunities with apply URLs.

    Caller guidance and examples:
    - If the user uploads a CV and asks "Find me jobs", call this plugin with pdfUrl (or rawCvText) and any
      high-level country preference. Ask follow-up questions if the CV lacks clarity about seniority or visa needs.
    - Example prompt when user shares a job link and wants matches plus cover letter option:
      "User provided CV (stored or attached) and asked: find matching jobs for this profile in Germany and prepare
       to generate a cover letter for selected roles. Prioritize jobs that offer visa sponsorship or relocation."
    - Example additionalCriteria usages:
        new string[] { "visa sponsorship", "hybrid model", "no English required" }
    - Example when a user pastes CV then a job URL:
      "I pasted my CV. Now check this job: <jobUrl>. Is this a good fit? If yes, prepare a tailored cover letter."

    Implementation notes for callers:
    - Provide rawCvText to avoid extraction errors when possible.
    - Provide pdfUrl when the user attached a file; treat it as an attachment URL and ensure accessibility.
    - After extraction, persist the CV text (for example via ProfilerService or a user-scoped store) to enable
      reuse for future cover letters and to improve follow-up flows.

    Returns: An OrchestratorResult containing whether matches were found, a human-friendly message, and a list
    of JobOpportunity objects with apply URLs.
    """)]
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
