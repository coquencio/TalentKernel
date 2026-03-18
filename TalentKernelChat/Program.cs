using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using TalentKernel.Plugins;
using TalentKernelChat;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
var modelKey = builder.Configuration["Model:key"] ?? string.Empty;
var adzunaId = builder.Configuration["Adzuna:AppId"] ?? string.Empty;
var adzunaKey = builder.Configuration["Adzuna:ApiKey"] ?? string.Empty;
var model = builder.Configuration["Model:deploymentName"] ?? string.Empty;
var modelEndpoint = builder.Configuration["Model:endpoint"] ?? string.Empty;
var discordToken = builder.Configuration["Discord:Token"] ?? string.Empty;

// HTTP Clients
builder.Services.AddHttpClient("AdzunaClient");
builder.Services.AddHttpClient("JinaReaderClient");

// Plugins Registration
builder.Services.AddSingleton(sp =>
    new JobSearchPlugin(adzunaId, adzunaKey, sp.GetRequiredService<IHttpClientFactory>()));
builder.Services.AddSingleton(sp =>
    new MarkdownBatchReaderPlugin(sp.GetRequiredService<IHttpClientFactory>()));
builder.Services.AddSingleton(sp =>
    new FileExtractorPlugin(sp.GetRequiredService<IHttpClientFactory>()));

builder.Services.AddSingleton<JobAnalystPlugin>();
builder.Services.AddSingleton<ProfilerPlugin>();
builder.Services.AddSingleton<ApplicationArchitectPlugin>();

// Kernel Registration
builder.Services.AddKeyedSingleton("talentKernel", (sp, key) => {
    var k = Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(model, modelEndpoint, modelKey, serviceId: "talentKernel")
        .Build();

    k.Plugins.AddFromObject(sp.GetRequiredService<JobSearchPlugin>());
    k.Plugins.AddFromObject(sp.GetRequiredService<MarkdownBatchReaderPlugin>());
    k.Plugins.AddFromObject(sp.GetRequiredService<JobAnalystPlugin>());
    k.Plugins.AddFromObject(sp.GetRequiredService<ProfilerPlugin>());
    k.Plugins.AddFromObject(sp.GetRequiredService<ApplicationArchitectPlugin>());
    k.Plugins.AddFromObject(sp.GetRequiredService<FileExtractorPlugin>());

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