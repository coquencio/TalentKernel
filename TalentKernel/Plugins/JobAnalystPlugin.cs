using Microsoft.SemanticKernel;
using System.ComponentModel;
using TalentKernel.Models;

namespace TalentKernel.Plugins;
public class JobAnalystPlugin
{
    private readonly MarkdownReaderPlugin _reader;
    // Shared prompt template used by both batch and single-job analysis methods.
    private static readonly string AnalysisPrompt = """
            You are a precise job analyst. For each provided job (already converted to Markdown), analyze the content
            and perform a semantic search for the provided criteria. The criteria array may include attribute-style
            filters such as "visa sponsorship", "relocation support", "hybrid model", "no English required",
            or information-extraction requests such as "years of experience", "required skills", "seniority level".

            For each job, return a JSON object with the following structure. Populate FoundDetails with any
            extracted structured information (only include keys when you can confidently extract them):
            [
              {
                "JobId": "string",
                "MeetsCriteria": boolean,          // true if the job meets ALL provided criteria
                "ConfidenceScore": double,        // 0.0 - 1.0, confidence that MeetsCriteria is correct
                "Reasoning": "string",          // short human-readable explanation
                "FoundDetails": {                 // map of extracted facts, optional keys below
                  "ApplyUrl": "string",
                  "YearsOfExperience": "string", // e.g. "3-5 years" or "Senior (5+)", or empty if not found
                  "RelocationSupport": "yes/no/unknown",
                  "VisaSponsorship": "yes/no/unknown",
                  "Languages": "string",         // e.g. "English required", "German preferred"
                  "Seniority": "string",         // e.g. "Junior", "Mid", "Senior"
                  "EmploymentType": "string"     // e.g. "Full time", "Contract", "Internship"
                }
              }
            ]

            Criteria:
            {{$criteria}}

            Jobs (for analysis):
            {{$jobs}}

            Notes and examples:
            - If the user asks "does this job offer relocation support?", the criteria will include "relocation support".
              You should set MeetsCriteria true if the job explicitly mentions relocation, relocation assistance, or
              relocation package. If it mentions only "relocation may be considered" you can set ConfidenceScore lower.
            - For "years of experience", extract any numeric ranges or explicit mentions like "3+ years" and put them
              in YearsOfExperience in FoundDetails.
            - If a criterion cannot be confidently determined, set MeetsCriteria to false and ConfidenceScore to a low value,
              but still populate FoundDetails when partial evidence exists.

            CRITICAL OUTPUT REQUIREMENTS:
            - Return ONLY raw JSON.
            - Do NOT wrap the JSON in markdown or code fences.
            - Do NOT include any explanation or text before or after the JSON.
            - The response must be valid JSON that can be parsed directly.
            """;

    public JobAnalystPlugin(MarkdownReaderPlugin reader)
    {
        _reader = reader;
    }

    public async Task<List<SemanticAnalystResult>> AnalyzeJobsBatch(
        Kernel kernel,
        [Description("A list of job data including ID and Markdown content")] List<JobContent> jobs,
        [Description("Criteria to validate, e.g., 'Visa sponsorship', 'Remote', 'relocation support', 'years of experience'")] string[] criteria)
    {
        var prompt = AnalysisPrompt;

        var arguments = new KernelArguments
        {
            { "jobs", System.Text.Json.JsonSerializer.Serialize(jobs) },
            { "criteria", string.Join(", ", criteria) }
        };

        var result = await kernel.InvokePromptAsync<string>(prompt, arguments);

        var data = System.Text.Json.JsonSerializer.Deserialize<List<SemanticAnalystResult>>(result!)
               ?? new List<SemanticAnalystResult>();

        return data;
    }

    [KernelFunction]
    [Description("""
    Read a single job URL, extract its Markdown content, and analyze it against the provided semantic criteria.

    This method uses the same analysis prompt as AnalyzeJobsBatch and returns a single SemanticAnalystResult
    describing whether the job meets the provided criteria, a confidence score, reasoning, and any extracted
    structured details (years of experience, relocation/visa signals, languages, seniority, apply URL, etc.).

    Usage examples:
    - "Does this job offer relocation support?" -> criteria = ["relocation support"]
    - "How many years of experience does this role require?" -> criteria = ["years of experience"]
    - "Is visa sponsorship available for this role and is English required?" -> criteria = ["visa sponsorship", "no English required"]

    Implementation notes:
    - The URL will be fetched and converted to Markdown by the MarkdownReaderPlugin before analysis.
    - The method relies on the shared analysis prompt to ensure consistent structured output and scoring.
    """)]
    public async Task<SemanticAnalystResult> AnalyzeSingleJobUrl(
        Kernel kernel,
        [Description("The full URL to analyze (absolute)")] string url,
        [Description("Criteria to validate, e.g., 'Visa sponsorship', 'Remote', 'german required', 'Full time'")] string[] criteria)
    {
        // Use the Markdown reader to fetch the URL content
        var content = await _reader.ReadUrlAsMarkdown(url);

        var jobs = new List<JobContent> { new JobContent(content.Id, content.Markdown) };

        var results = await this.AnalyzeJobsBatch(kernel, jobs, criteria);

        // Return the analysis result for the single job (or a negative result if none matched)
        if (results != null && results.Count > 0)
        {
            return results.First();
        }

        return new SemanticAnalystResult
        {
            MeetsCriteria = false,
            ConfidenceScore = 0.0,
            Reasoning = "No criteria matched.",
            FoundDetails = new()
        };
    }
}

// Helper class for the batch
public record JobContent(string Id, string Markdown);

