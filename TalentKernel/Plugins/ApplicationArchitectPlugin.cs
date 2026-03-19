using Microsoft.SemanticKernel;
using System.ComponentModel;
using TalentKernel.Models;
using TalentKernel.Services;
using System.Linq;

namespace TalentKernel.Plugins;
public class ApplicationArchitectPlugin
{
    private readonly MarkdownReaderPlugin _reader;
    private readonly FileExtractorService _fileExtractor;
    private readonly ProfilerService _profiler;

    public ApplicationArchitectPlugin(MarkdownReaderPlugin reader, FileExtractorService fileExtractor, ProfilerService profiler)
    {
        _reader = reader;
        _fileExtractor = fileExtractor;
        _profiler = profiler;
    }

    [KernelFunction]
    [Description("Generates a factual cover letter based on a candidate CV (raw text or PDF URL) and a specific job URL. The plugin will extract/profile the CV when needed and read the job URL to build the letter.")]
    public async Task<CoverLetterResult> GenerateCoverLetter(
        Kernel kernel,
        [Description("Public URL to a CV PDF (optional if rawCvText is provided)")] string? pdfUrl,
        [Description("Raw CV text pasted directly (optional if pdfUrl is provided)")] string? rawCvText,
        [Description("The full URL of the job vacancy (absolute)")] string jobUrl,
        [Description("Optional personal notes or specific motivations for this application")] string? personalNotes = null)
    {
        // 1. Obtain resume text: raw text preferred; otherwise extract from PDF
        string resumeText;
        if (!string.IsNullOrWhiteSpace(rawCvText))
        {
            resumeText = rawCvText!;
        }
        else if (!string.IsNullOrWhiteSpace(pdfUrl))
        {
            resumeText = await _fileExtractor.ExtractTextFromPdf(pdfUrl!);
            if (string.IsNullOrWhiteSpace(resumeText) || resumeText.StartsWith("The downloaded file is not a PDF."))
            {
                return new CoverLetterResult
                {
                    Content = "Could not extract a valid PDF from the provided URL.",
                    MatchingSkillsUsed = new()
                };
            }
        }
        else
        {
            return new CoverLetterResult
            {
                Content = "No CV provided. Please supply either a PDF URL or paste the CV text.",
                MatchingSkillsUsed = new()
            };
        }

        // 2. Build candidate profile using the ProfilerService
        var profile = await _profiler.ParseResume(kernel, resumeText);

        // 3. Use the reader to fetch the job content as markdown
        var content = await _reader.ReadUrlAsMarkdown(jobUrl);
        var jobMarkdown = content?.Markdown ?? string.Empty;

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

        var result = await kernel.InvokePromptAsync<string>(prompt, new()
        {
            { "profile", profile },
            { "jobMarkdown", jobMarkdown },
            { "personalNotes", personalNotes ?? "No specific notes provided." }
        });

        return new CoverLetterResult
        {
            Content = result ?? string.Empty,
            MatchingSkillsUsed = profile.CoreSkills?.Where(s => jobMarkdown.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList() ?? new()
        };
    }
}

