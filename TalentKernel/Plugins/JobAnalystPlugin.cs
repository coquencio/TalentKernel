using Microsoft.SemanticKernel;
using System.ComponentModel;
using TalentKernel.Models;

namespace TalentKernel.Plugins;
public class JobAnalystPlugin
{
    [KernelFunction]
    [Description("Analyzes a collection of job descriptions against specific criteria in a single pass to save tokens.")]
    public async Task<List<SemanticAnalystResult>> AnalyzeJobsBatch(
        Kernel kernel,
        [Description("A list of job data including ID and Markdown content")] List<JobContent> jobs,
        [Description("Criteria to validate, e.g., 'Visa sponsorship', 'Remote'")] string[] criteria)
    {
        // En un solo prompt enviamos todos los trabajos
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

        return System.Text.Json.JsonSerializer.Deserialize<List<SemanticAnalystResult>>(result!)
               ?? new List<SemanticAnalystResult>();
    }
}

// Helper class for the batch
public record JobContent(string Id, string Markdown);

