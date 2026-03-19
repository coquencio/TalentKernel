using Microsoft.SemanticKernel;
using System.ComponentModel;
using TalentKernel.Models;

namespace TalentKernel.Plugins;
public class JobAnalystPlugin
{
    private readonly MarkdownReaderPlugin _reader;

    public JobAnalystPlugin(MarkdownReaderPlugin reader)
    {
        _reader = reader;
    }

    public async Task<List<SemanticAnalystResult>> AnalyzeJobsBatch(
        Kernel kernel,
        [Description("A list of job data including ID and Markdown content")] List<JobContent> jobs,
        [Description("Criteria to validate, e.g., 'Visa sponsorship', 'Remote'")] string[] criteria)
    {
        var prompt = """
            Analyze the following {{jobs.Count}} job descriptions against these criteria: {{criteria}}.
            
            Jobs:
            {{#each jobs}}
            ID: {{this.Id}}
            Content: {{this.Markdown}}
            ---
            {{/each}}

            Return a JSON array of objects with this structure:
            [
              {
                "JobId": "string",
                "MeetsCriteria": boolean,
                "ConfidenceScore": double,
                "Reasoning": "string"
              }
            ]
            """;

        var arguments = new KernelArguments
        {
            { "jobs", jobs },
            { "criteria", string.Join(", ", criteria) }
        };

        var result = await kernel.InvokePromptAsync<string>(prompt, arguments);

        var data = System.Text.Json.JsonSerializer.Deserialize<List<SemanticAnalystResult>>(result!)
               ?? new List<SemanticAnalystResult>();

        return data.Where(r => r.MeetsCriteria).ToList();
    }

    [KernelFunction]
    [Description("Reads a single URL and analyzes its content against the provided criteria.")]
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

