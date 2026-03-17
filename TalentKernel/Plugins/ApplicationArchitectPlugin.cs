using Microsoft.SemanticKernel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using TalentKernel.Models;

namespace TalentKernel.Plugins;
public class ApplicationArchitectPlugin
{
    [KernelFunction]
    [Description("Generates a factual cover letter based on the candidate's profile and a specific job description.")]
    public async Task<CoverLetterResult> GenerateCoverLetter(
        Kernel kernel,
        [Description("The structured profile of the candidate")] CandidateProfile profile,
        [Description("The markdown content of the job vacancy")] string jobMarkdown,
        [Description("Optional personal notes or specific motivations for this application")] string? personalNotes = null)
    {
        var prompt = """
            Create a professional and concise cover letter for {{profile.FullName}}.
            
            STRICT RULES:
            1. Use ONLY the skills and experience listed in the Candidate Profile. 
            2. DO NOT invent previous job titles, degrees, or certifications.
            3. If a requirement in the Job Description is not in the Profile, do not mention it.
            4. Incorporate the Personal Notes to explain "The Why" behind the application.

            Candidate Profile:
            - Skills: {{profile.CoreSkills}}
            - Experience: {{profile.YearsOfExperience}} years
            - Summary: {{profile.Summary}}

            Personal Notes from Candidate:
            {{personalNotes}}

            Job Description:
            {{jobMarkdown}}

            Return the letter in a professional format.
            """;

        var result = await kernel.InvokePromptAsync<string>(prompt, new() {
            { "profile", profile },
            { "jobMarkdown", jobMarkdown },
            { "personalNotes", personalNotes ?? "No specific notes provided." }
        });

        return new CoverLetterResult
        {
            Content = result ?? string.Empty,
            MatchingSkillsUsed = profile.CoreSkills.Where(s => jobMarkdown.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList()
        };
    }
}

