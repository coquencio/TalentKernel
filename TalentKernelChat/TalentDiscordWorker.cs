using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureAIInference;
using System.Text;

namespace TalentKernelChat;

public class TalentDiscordWorker : BackgroundService
{
    private readonly DiscordSocketClient _client;
    private readonly Kernel _kernel;
    private readonly string _token;
    private readonly string _promptyPath;
    private readonly ChatHistory _chatHistory;

    public TalentDiscordWorker(
        DiscordSocketClient client,
        [FromKeyedServices("talentKernel")] Kernel kernel,
        IConfiguration config)
    {
        _client = client;
        _kernel = kernel;
        _token = config["Discord:Token"] ?? string.Empty;
        _chatHistory = new ChatHistory();

#if DEBUG
        string projectRoot = Directory.GetParent(AppContext.BaseDirectory)?.Parent?.Parent?.Parent?.FullName ?? string.Empty;
        _promptyPath = Path.Combine(projectRoot, "Prompts", "TalentKernel.prompty");
#else
        _promptyPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "TalentKernel.prompty");
#endif
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

        StringBuilder userContent = new StringBuilder(message.Content);
        var attachment = message.Attachments.FirstOrDefault(a => a.Filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

        if (attachment != null)
        {
            userContent.AppendLine($"\n[File Attached URL: {attachment.Url}]");
        }

        try
        {
            var promptyContent = await File.ReadAllTextAsync(_promptyPath);
            var promptyFunction = _kernel.CreateFunctionFromPrompty(promptyContent);

            var settings = new AzureAIInferencePromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                MaxTokens = 800
            };

            var historyBuilder = new StringBuilder();
            foreach (var chatMessage in _chatHistory)
            {
                historyBuilder.AppendLine($"{chatMessage.Role}: {chatMessage.Content}");
            }

            var arguments = new KernelArguments(settings)
            {
                ["user_input"] = userContent.ToString(),
                ["chat_history"] = historyBuilder.ToString()
            };

            var result = await _kernel.InvokeAsync(promptyFunction, arguments);
            string responseText = result.ToString();

            if (!string.IsNullOrWhiteSpace(responseText))
            {
                _chatHistory.AddUserMessage(userContent.ToString());
                _chatHistory.AddAssistantMessage(responseText);

                foreach (var chunk in responseText.Chunk(1900))
                {
                    await message.Channel.SendMessageAsync(new string(chunk.ToArray()));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FATAL: {ex.Message}");
            await message.Channel.SendMessageAsync("An error occurred during kernel execution.");
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