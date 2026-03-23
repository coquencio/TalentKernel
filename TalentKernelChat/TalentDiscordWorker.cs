using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Collections.Concurrent;

namespace TalentKernelChat;

public class TalentDiscordWorker : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly string _token;
    private readonly ConcurrentDictionary<ulong, ChatHistory> _userHistories = new();

    private readonly OpenAIPromptExecutionSettings _executionSettings = new()
    {
        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true),
        Temperature = 0.1
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
    }

    private ChatHistory GetHistoryForUser(ulong userId)
    {
        return _userHistories.GetOrAdd(userId, _ => new ChatHistory("""
            # ROLE
            You are 'TalentKernel', an autonomous career agent for Software Engineers. 
            
            # REASONING PROTOCOL
            1. ANALYZE user input for tool requirements.
            2. EXECUTE tools immediately. DO NOT ask for permission.
            3. DO NOT announce which tool you will use.
            4. NEVER write 'tool_call_name' or 'tool_call_arguments' in your response text.
            5. If a PDF/CV is present: Run 'CvOrchestratorPlugin-OrchestrateCvJobSearch' now.
            
            # DIRECTIVES
            - Be concise. 
            - Use tools first, talk later.
            - If a CV is processed, persist it to memory automatically.
            """));
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
        var history = GetHistoryForUser(message.Author.Id);

        string userContent = message.Content;
        var attachment = message.Attachments.FirstOrDefault(a => a.Filename.EndsWith(".pdf"));

        if (attachment != null)
        {
            userContent += $"\n[COMMAND: PDF Attached. Name: {attachment.Filename}. URL: {attachment.Url}. ANALYZE AND EXECUTE PLUGINS NOW.]";
        }

        history.AddUserMessage(userContent);

        try
        {
            var response = await _chatService.GetChatMessageContentAsync(history, _executionSettings, _kernel);

            if (!string.IsNullOrEmpty(response.Content))
            {
                history.Add(response);
                foreach (var chunk in response.Content.Chunk(1900))
                {
                    await message.Channel.SendMessageAsync(new string(chunk.ToArray()));
                }
            }
        }
        catch (Exception ex)
        {
            await message.Channel.SendMessageAsync($"⚠️ Error: {ex.Message}");
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