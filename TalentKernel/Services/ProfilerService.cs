
using Microsoft.SemanticKernel;
using System.Text.Json;
using TalentKernel.Models;

namespace TalentKernel.Services;

public class ProfilerService
{
    public async Task<CandidateProfile> ParseResume(
        Kernel kernel,
        string rawResumeText)
    {
        var prompt = """
            Extract the professional profile from the following resume text.
            IMPORTANT: Persist the plain-text CV to memory for future reference, but only return the structured profile information as JSON.
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
