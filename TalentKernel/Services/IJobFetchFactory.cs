using System;
using System.Threading.Tasks;
using TalentKernel.Plugins;

namespace TalentKernel.Services;

public interface IJobFetchFactory
{
    /// <summary>
    /// Create an IJobFetch implementation for the requested source.
    /// </summary>
    /// <param name="source">Identifier for the job source (e.g., "adzuna").</param>
    /// <param name="appKey">Optional app key for providers that require it.</param>
    /// <param name="appId">Optional app id for providers that require it.</param>
    /// <param name="httpClientFactory">Optional IHttpClientFactory used to create provider-specific clients.</param>
    IJobFetch Create(string source, string? appKey = null, string? appId = null, IHttpClientFactory? httpClientFactory = null);
}
