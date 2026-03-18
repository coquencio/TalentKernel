using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace TalentKernelChat;
public class TalentDiscordWorker : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly string _token;
    private readonly ChatHistory _chatHistory;
    // Define the execution settings here to fix the "context" error
    private readonly OpenAIPromptExecutionSettings _executionSettings = new()
    {
        ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
        ServiceId = "talentKernel"
    };

    public TalentDiscordWorker(
        DiscordSocketClient client,
        [FromKeyedServices("talentKernel")] Kernel kernel,
        IConfiguration config)
    {
        _client = client;
        _kernel = kernel;
        _chatService = kernel.GetRequiredService<IChatCompletionService>();
        _token = config["Discord:Token"] ?? string.Empty;

        _chatHistory = new ChatHistory("""
            You are 'TalentKernel', a high-end career agent for software engineers.
            Your goal is to find jobs that offer visa sponsorship and match the user's profile perfectly.
            
            FLOW RULES:
            1. If the user provides a CV (as text or a PDF URL), use 'ProfileExpertPlugin' to structure it.
            2. When searching, always use the search terms derived from the CV.
            3. After getting raw results from Adzuna, ALWAYS use 'MarkdownBatchReaderPlugin' followed by 'JobAnalystPlugin' to verify visa and requirements before showing results.
            4. Only show jobs that have a 'ConfidenceScore' higher than 0.6.
            5. Keep the Job ID and Markdown content in memory to generate cover letters later.
            6. If a Discord attachment URL is provided, ALWAYS use 'FileExtractorPlugin' to read the content before analyzing it.

            GUIDELINES FOR SINGLE LINKS:
            1. If a user provides a single URL (webpage), ALWAYS use 'ReadSingleJob' first to understand the requirements.
            2. If they ask for a cover letter or summary for that link, use the retrieved Markdown content.
            3. If 'ReadSingleJob' returns an error, inform the user and ask for the text.
            """);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.MessageReceived += OnMessageReceived;

        await _client.LoginAsync(TokenType.Bot, _token);
        await _client.StartAsync();

        await Task.Delay(-1, stoppingToken);
    }

    private async Task OnMessageReceived(SocketMessage message)
    {
        if (message.Author.IsBot) return;

        using var typing = message.Channel.EnterTypingState();

        string userContent = message.Content;

        // Handle Attachments
        if (message.Attachments.Any())
        {
            var file = message.Attachments.First();
            if (file.Filename.EndsWith(".pdf"))
            {
                // We tell the LLM there is a file and give it the URL
                userContent += $"\n[File Attached: {file.Filename}, URL: {file.Url}]";
                userContent += "\nPlease analyze this document.";
            }
        }

        _chatHistory.AddUserMessage(userContent);

        var result = await _chatService.GetChatMessageContentAsync(_chatHistory, _executionSettings, _kernel);

        if (!string.IsNullOrEmpty(result.Content))
        {
            _chatHistory.AddAssistantMessage(result.Content);

            // Discord limit is 2000, we use 1900 to be safe
            var chunks = result.Content.Chunk(1900);

            foreach (var chunk in chunks)
            {
                await message.Channel.SendMessageAsync(chunk.ToString());
            }
        }
    }
}
public static class StringExtensions
{
    public static IEnumerable<string> Chunk(this string str, int chunkSize)
    {
        for (int i = 0; i < str.Length; i += chunkSize)
            yield return str.Substring(i, Math.Min(chunkSize, str.Length - i));
    }
}

