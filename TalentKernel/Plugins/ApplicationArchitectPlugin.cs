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
    [Description("""
    Generates a factual, professional cover letter based on a candidate CV (raw text or PDF attachment URL) and a specific job URL.

    Behavior and priorities:
    - Prefer rawCvText when supplied directly by the user. If rawCvText is empty and pdfUrl is provided, the plugin will attempt to extract plain text from the PDF attachment.
    - Treat any PDF URL provided as a Discord attachment URL (or other public attachment URL) and attempt to download and extract text.
    - If the plugin successfully extracts the CV as plain text, persist the extracted CV to memory (associated with the user) so it can be automatically reused for future cover letter requests.
      Confirm storage to the user unless they explicitly opt out.
    - Do NOT invent facts. Only use skills, roles, dates, certifications, and experience that appear in the candidate profile or CV text.

    Output contract:
    - Return a concise, professional cover letter tailored to the job description content.
    - Also provide a list of matching skills used (so callers can present which skills were matched against the job).

    Strict rules enforced by this plugin:
    1) Use ONLY the skills and experiences present in the parsed candidate profile / CV text.
    2) Never fabricate previous job titles, degrees, certifications, or outcomes.
    3) If the job description requires something not present in the profile, omit it from the letter.
    4) Incorporate personalNotes when provided to explain the candidate's motivation.

    Example prompts and usage scenarios (copy these examples to the LLM prompt when instructing the model):
    - If a user shares a link and asks for cover letter generation:
      "The user shared this job link: <jobUrl>. Please generate a professional cover letter using the CV stored for this user. If you cannot find a stored CV, ask the user to upload or paste their CV."

    - If a user shares his CV text and then a job URL:
      "User: Here is my CV: <paste CV text>. Now please write a concise cover letter for this job: <jobUrl>. Emphasize backend engineering experience and cloud migrations."

    - If a user uploads a PDF attachment and asks for a cover letter:
      "User attached resume.pdf. Please extract the text from the attached PDF, profile the candidate, persist the plain-text CV to memory, and generate a customized cover letter for: <jobUrl>. Confirm that you stored the CV for future use."

    - If a user provides personal notes or goals:
      "User: I'm transitioning from QA to backend engineering; highlight transferable skills and willingness to learn. Use my CV (stored or attached) to write a tailored cover letter for: <jobUrl>."

    - Template prompt to send to the LLM when composing the letter:
      "Create a professional cover letter for {{profile.FullName}} using only information from the Profile. Job description: {{jobMarkdown}}. Personal notes: {{personalNotes}}. Follow the strict rules: no invention of facts; be concise; highlight matching skills."

    Implementation notes for callers:
    - Provide rawCvText when available to avoid extraction errors.
    - Provide pdfUrl for attached PDFs; caller should treat PDF URLs as attachment URLs and guarantee they are reachable by the service.
    - After extraction, persist the CV text (for example via the ProfilerService or a user-scoped store) so subsequent requests for cover letters can reuse it automatically.
    """)]
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

