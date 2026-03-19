using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using TalentKernel.Extensions;
using TalentKernelChat;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
var discordToken = builder.Configuration["Discord:Token"] ?? string.Empty;

// Register all TalentKernel services, plugins and kernel
builder.Services.AddTalentKernelServices(builder.Configuration);

// Kernel Registration
var modelKey = builder.Configuration["Model:key"] ?? string.Empty;
var model = builder.Configuration["Model:deploymentName"] ?? string.Empty;
var modelEndpoint = builder.Configuration["Model:endpoint"] ?? string.Empty;

builder.Services.AddKeyedSingleton("talentKernel", (sp, key) =>
{
    var k = Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(model, modelEndpoint, modelKey, serviceId: "talentKernel")
        .Build();

    k.AddTalentPlugins(sp);

    return k;
});

// Discord Client Setup
builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
}));

// Background Service
builder.Services.AddHostedService<TalentDiscordWorker>();

using var host = builder.Build();
await host.RunAsync();