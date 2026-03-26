using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using TalentKernel.Plugins;
using TalentKernel.Services;

namespace TalentKernel.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTalentKernelServices(this IServiceCollection services, IConfiguration configuration)
    {
        var adzunaId = configuration["Adzuna:AppId"] ?? string.Empty;
        var adzunaKey = configuration["Adzuna:ApiKey"] ?? string.Empty;

        // HTTP clients
        services.AddHttpClient("AdzunaClient");
        services.AddHttpClient("JinaReaderClient");

        // Service registrations
        services.AddSingleton<FileExtractorService>();
        services.AddSingleton<ProfilerService>();

        // Plugin registrations (As Singletons to be resolved by the Kernel)
        // Register the factory and the main plugin that will use it
        services.AddSingleton<IJobFetchFactory, DefaultJobFetchFactory>();
        services.AddSingleton(sp =>
            new JobSearchPlugin(adzunaKey, adzunaId, sp.GetRequiredService<IHttpClientFactory>(), sp.GetRequiredService<IJobFetchFactory>()));

        services.AddSingleton<MarkdownReaderPlugin>();
        services.AddSingleton<CvOrchestratorPlugin>();
        services.AddSingleton<JobAnalystPlugin>();
        services.AddSingleton<ApplicationArchitectPlugin>();

        return services;
    }

    /// <summary>
    /// Registers plugins with EXPLICIT names to ensure the LLM 
    /// can find them consistently without "hallucinating" aliases.
    /// </summary>
    public static void AddTalentPlugins(this Kernel kernel, IServiceProvider sp)
    {
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));
        if (sp == null) throw new ArgumentNullException(nameof(sp));

        kernel.Plugins.AddFromObject(sp.GetRequiredService<JobSearchPlugin>(), "JobSearchPlugin");
        kernel.Plugins.AddFromObject(sp.GetRequiredService<MarkdownReaderPlugin>(), "MarkdownReaderPlugin");
        kernel.Plugins.AddFromObject(sp.GetRequiredService<JobAnalystPlugin>(), "JobAnalystPlugin");
        kernel.Plugins.AddFromObject(sp.GetRequiredService<ApplicationArchitectPlugin>(), "ApplicationArchitectPlugin");
        kernel.Plugins.AddFromObject(sp.GetRequiredService<CvOrchestratorPlugin>(), "CvOrchestratorPlugin");
    }
}