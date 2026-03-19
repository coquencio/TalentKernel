using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using System;
using TalentKernel.Plugins;
using TalentKernel.Services;

namespace TalentKernel.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all TalentKernel related services, HTTP clients, plugins and the named kernel instance.
    /// Reads configuration values from the provided IConfiguration instance.
    /// </summary>
    public static IServiceCollection AddTalentKernelServices(this IServiceCollection services, IConfiguration configuration)
    {
        var modelKey = configuration["Model:key"] ?? string.Empty;
        var adzunaId = configuration["Adzuna:AppId"] ?? string.Empty;
        var adzunaKey = configuration["Adzuna:ApiKey"] ?? string.Empty;
        var model = configuration["Model:deploymentName"] ?? string.Empty;
        var modelEndpoint = configuration["Model:endpoint"] ?? string.Empty;

        // HTTP clients
        services.AddHttpClient("AdzunaClient");
        services.AddHttpClient("JinaReaderClient");

        // Plugin & service registrations
        services.AddSingleton<JobSearchPlugin>(sp => new JobSearchPlugin(adzunaId, adzunaKey, sp.GetRequiredService<IHttpClientFactory>()));
        services.AddSingleton<MarkdownReaderPlugin>();
        services.AddSingleton<FileExtractorService>();
        services.AddSingleton<CvOrchestratorPlugin>();

        services.AddSingleton<JobAnalystPlugin>();
        services.AddSingleton<ProfilerService>();
        services.AddSingleton<ApplicationArchitectPlugin>();

        // Note: kernel creation (and any connector-specific calls such as
        // AddAzureOpenAIChatCompletion) should be done in the application
        // project where the connector packages are referenced. This method
        // only registers HTTP clients, plugins and services.

        return services;
    }

    /// <summary>
    /// Adds all TalentKernel plugins to the provided kernel using services from the service provider.
    /// Use this to register plugins in one call: kernel.AddTalentPlugins(sp)
    /// </summary>
    public static void AddTalentPlugins(this Kernel kernel, IServiceProvider sp)
    {
        if (kernel == null) throw new ArgumentNullException(nameof(kernel));
        if (sp == null) throw new ArgumentNullException(nameof(sp));

        kernel.Plugins.AddFromObject(sp.GetRequiredService<JobSearchPlugin>());
        kernel.Plugins.AddFromObject(sp.GetRequiredService<MarkdownReaderPlugin>());
        kernel.Plugins.AddFromObject(sp.GetRequiredService<JobAnalystPlugin>());
        kernel.Plugins.AddFromObject(sp.GetRequiredService<ApplicationArchitectPlugin>());
        kernel.Plugins.AddFromObject(sp.GetRequiredService<CvOrchestratorPlugin>());
    }
}
