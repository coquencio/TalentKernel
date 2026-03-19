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
            1. If the user provides a CV (as text or as a PDF attachment), and job criteria, use the CvOrchestratorPlugin to extract and parse the CV and find job matches.
               - Note: any PDF URL in messages should be treated as a Discord attachment URL.
            2. If the user provides a job URL, a PDF attachment, or CV information and asks for a cover letter, use the ApplicationArchitectPlugin.
            3. Persist CV data: whenever a plugin extracts the user's CV as plain text (for example after processing an attached PDF),
               store the extracted CV text in memory associated with the user so it can be reused later.
               - On subsequent requests to write a cover letter, load the stored CV from memory and set it as the user's CV context before composing.
            4. If the user provides job criteria (e.g. "I want a remote job in Germany that offers visa sponsorship"), use the JobSearchPlugin to find relevant jobs.
            5. If the user provides URLs, use the MarkdownReaderPlugin to read and analyze them.
            6. If the user provides job URL and ask specific questions about it, use the JobAnalystPlugin.

            ADDITIONAL GUIDELINES:
            - When a PDF is attached, prefer extracting and profiling the text. If extraction succeeds, persist the plain-text CV to memory.
            - Always confirm when you have stored a CV to memory and explain how it will be used for future cover letters, unless the user opts out.
            - Treat any reference to a PDF URL as a Discord attachment URL and attempt to retrieve and analyze the attachment when possible.

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
        if (message.Attachments.Any(a => a.Filename.EndsWith(".pdf")))
        {
            var file = message.Attachments.First();
            userContent += $"\n[File Attached: {file.Filename}, URL: {file.Url}]";
        }

        _chatHistory.AddUserMessage(userContent);

        var result = await _chatService.GetChatMessageContentAsync(_chatHistory, _executionSettings, _kernel);

        _chatHistory.Add(result);

        if (!string.IsNullOrEmpty(result.Content))
        {
            var chunks = result.Content.Chunk(1900);
            foreach (var chunk in chunks)
            {
                await message.Channel.SendMessageAsync(new string(chunk.ToArray()));
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

