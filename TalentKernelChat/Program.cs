using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using TalentKernel.Extensions;
using TalentKernelChat;

var builder = Host.CreateApplicationBuilder(args);

var discordToken = builder.Configuration["Discord:Token"] ?? string.Empty;

builder.Services.AddTalentKernelServices(builder.Configuration);

var modelKey = builder.Configuration["Model:key"] ?? string.Empty;
var model = builder.Configuration["Model:deploymentName"] ?? string.Empty;
var modelEndpoint = builder.Configuration["Model:endpoint"] ?? string.Empty;

builder.Services.AddKeyedSingleton("talentKernel", (sp, key) =>
{
    var kb = Kernel.CreateBuilder();
    var endpointUri = new Uri(modelEndpoint);
    var isAzureOpenAiHost = endpointUri.Host.EndsWith(".openai.azure.com", StringComparison.OrdinalIgnoreCase);

    if (isAzureOpenAiHost)
    {
        var resourceBase = new Uri($"{endpointUri.Scheme}://{endpointUri.Host}/");
        kb.AddAzureOpenAIChatCompletion(deploymentName: model, endpoint: resourceBase.ToString(), apiKey: modelKey);
    }
    else if (endpointUri.AbsolutePath.Contains("/openai/", StringComparison.OrdinalIgnoreCase))
    {
        kb.AddOpenAIChatCompletion(modelId: model, endpoint: endpointUri, apiKey: modelKey);
    }
    else
    {
        var inferenceBase = endpointUri.AbsolutePath.EndsWith("/models", StringComparison.OrdinalIgnoreCase)
            ? endpointUri
            : new Uri(endpointUri.ToString().TrimEnd('/') + "/models");

        kb.AddAzureAIInferenceChatCompletion(modelId: model, apiKey: modelKey, endpoint: inferenceBase);
    }

    var k = kb.Build();
    k.AddTalentPlugins(sp);
    return k;
});

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
}));

builder.Services.AddHostedService<TalentDiscordWorker>();

using var host = builder.Build();
await host.RunAsync();