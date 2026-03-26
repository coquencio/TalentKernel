using System;
using TalentKernel.Services;

namespace TalentKernel.Plugins;

public class DefaultJobFetchFactory : IJobFetchFactory
{
    public IJobFetch Create(string source, string? appKey = null, string? appId = null, IHttpClientFactory? httpClientFactory = null)
    {
        if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("source is required", nameof(source));

        switch (source.Trim().ToLowerInvariant())
        {
            case "adzuna":
                if (httpClientFactory is null) throw new ArgumentNullException(nameof(httpClientFactory), "IHttpClientFactory is required for the Adzuna implementation.");
                return new AdzunaJobFetch(appKey ?? string.Empty, appId ?? string.Empty, httpClientFactory);
            default:
                throw new NotSupportedException($"Job fetch source '{source}' is not supported by the default factory.");
        }
    }
}
