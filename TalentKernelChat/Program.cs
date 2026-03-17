using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TalentKernel.Plugins;


// Setup Services & HttpClientFactory
var builder = Host.CreateApplicationBuilder();
var modelKey = builder.Configuration["Model:key"] ?? string.Empty;
var adzunaId = builder.Configuration["Adzuna:AppId"] ?? string.Empty;
var adzunaKey = builder.Configuration["Adzuna:ApiKey"] ?? string.Empty;
var model = builder.Configuration["Model:deploymentName"] ?? string.Empty;
var modelEndpoint = builder.Configuration["Model:endpoint"] ?? string.Empty;
builder.Services.AddHttpClient("AdzunaClient");
builder.Services.AddHttpClient("JinaReaderClient");

// Register your Plugins in the DI Container
builder.Services.AddSingleton(sp =>
    new JobSearchPlugin(adzunaId, adzunaKey, sp.GetRequiredService<IHttpClientFactory>()));
builder.Services.AddSingleton(sp =>
    new MarkdownBatchReaderPlugin(sp.GetRequiredService<IHttpClientFactory>()));
builder.Services.AddSingleton<JobAnalystPlugin>();
builder.Services.AddSingleton<ProfilerPlugin>();
builder.Services.AddSingleton<ApplicationArchitectPlugin>();

// Build the Kernel
var kernelBuilder = builder.Services.AddKeyedSingleton("talentKernel", (sp, key) => {
    var k = Kernel.CreateBuilder()
        .AddAzureOpenAIChatCompletion(model, modelEndpoint, modelKey)
        .Build();

    // Add all plugins to the Kernel
    k.Plugins.AddFromObject(sp.GetRequiredService<JobSearchPlugin>());
    k.Plugins.AddFromObject(sp.GetRequiredService<MarkdownBatchReaderPlugin>());
    k.Plugins.AddFromObject(sp.GetRequiredService<JobAnalystPlugin>());
    k.Plugins.AddFromObject(sp.GetRequiredService<ProfilerPlugin>());
    k.Plugins.AddFromObject(sp.GetRequiredService<ApplicationArchitectPlugin>());

    return k;
});

using var host = builder.Build();
var kernel = host.Services.GetRequiredKeyedService<Kernel>("talentKernel");
var chatService = kernel.GetRequiredService<IChatCompletionService>();

// Configuramos el historial con las reglas de negocio (System Prompt)
var chatHistory = new ChatHistory("""
    You are 'TalentKernel', a high-end career agent for software engineers.
    Your goal is to find jobs that offer visa sponsorship and match the user's profile perfectly.
    
    FLOW RULES:
    1. If the user provides a CV, use 'ProfileExpertPlugin' to structure it.
    2. When searching, always use the search terms derived from the CV.
    3. After getting raw results from Adzuna, ALWAYS use 'MarkdownBatchReaderPlugin' followed by 'JobAnalystPlugin' to verify visa and requirements before showing results.
    4. Only show jobs that have a 'ConfidenceScore' higher than 0.6.
    5. Keep the Job ID and Markdown content in memory to generate cover letters later.

    GUIDELINES FOR SINGLE LINKS:
    1. If a user provides a single URL, ALWAYS use 'ReadSingleJob' first to understand the requirements.
    2. If they ask for a cover letter or summary for that link, use the retrieved Markdown content.
    3. If 'ReadSingleJob' returns an error (timeout or blocked), inform the user politely and ask them to paste the job description text directly.
    """);

// Configuración para que el LLM llame a las funciones automáticamente
var executionSettings = new OpenAIPromptExecutionSettings
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
};

Console.WriteLine("--- Talent Agent Active ---");

while (true)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("\nUser: ");
    string input = Console.ReadLine()!;
    if (string.IsNullOrWhiteSpace(input)) continue;

    chatHistory.AddUserMessage(input);

    var response = await chatService.GetChatMessageContentAsync(chatHistory, executionSettings, kernel);

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\nVisaScout: {response.Content}");
    chatHistory.AddAssistantMessage(response.Content!);
}
