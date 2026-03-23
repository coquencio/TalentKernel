using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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
        _promptyPath = Path.Combine(AppContext.BaseDirectory, "Prompts", "TalentKernel.prompty");
        _chatHistory = new ChatHistory();
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
        if (message.Attachments.Any(a => a.Filename.EndsWith(".pdf")))
        {
            var file = message.Attachments.First();
            userContent.AppendLine($"\n[File Attached: {file.Url}]");
        }

        try
        {
            var promptyContent = await File.ReadAllTextAsync(_promptyPath);
            var promptyFunction = _kernel.CreateFunctionFromPrompty(promptyContent);

            var arguments = new KernelArguments
            {
                ["user_input"] = userContent.ToString(),
                ["chat_history"] = _chatHistory
            };

            var result = await _kernel.InvokeAsync(promptyFunction, arguments);
            string responseText = result.ToString();

            _chatHistory.AddUserMessage(userContent.ToString());
            _chatHistory.AddAssistantMessage(responseText);

            if (!string.IsNullOrEmpty(responseText))
            {
                var chunks = responseText.Chunk(1900);
                foreach (var chunk in chunks)
                {
                    await message.Channel.SendMessageAsync(new string(chunk.ToArray()));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            await message.Channel.SendMessageAsync("Lo siento, ocurrió un error procesando tu solicitud.");
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