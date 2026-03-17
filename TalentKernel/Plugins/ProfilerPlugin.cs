
using Microsoft.SemanticKernel;
using System.ComponentModel;
using System.Text.Json;
using TalentKernel.Models;

namespace TalentKernel.Plugins;

public class ProfilerPlugin
{
    [KernelFunction]
    [Description("Analyzes raw CV text and extracts structured professional information.")]
    public async Task<CandidateProfile> ParseResume(
        Kernel kernel,
        [Description("The raw text content of the user's CV or LinkedIn profile")] string rawResumeText)
    {
        var prompt = """
            Extract the professional profile from the following resume text.
            
            Resume Text:
            {{$rawResumeText}}

            Return a JSON object with this exact structure:
            {
              "FullName": "string",
              "CoreSkills": ["skill1", "skill2"],
              "YearsOfExperience": number,
              "PreferredRoles": ["role1", "role2"],
              "Summary": "A 2-sentence professional summary"
            }
            """;

        var result = await kernel.InvokePromptAsync<string>(prompt, new() { { "rawResumeText", rawResumeText } });

        return JsonSerializer.Deserialize<CandidateProfile>(result!) ?? new CandidateProfile();
    }
}
