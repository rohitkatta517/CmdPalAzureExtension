// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Serilog;

namespace AzureExtension.Client;

/// <summary>
/// Provides centralized URL building utilities for Azure DevOps resources.
/// This class handles the differences between modern (dev.azure.com) and legacy
/// (visualstudio.com) URL formats, ensuring consistent URL construction across
/// the application.
/// </summary>
/// <remarks>
/// Azure DevOps supports two URL formats:
/// <list type="bullet">
///   <item><description>Modern: https://dev.azure.com/{organization}/{project}/...</description></item>
///   <item><description>Legacy: https://{organization}.visualstudio.com/{project}/...</description></item>
/// </list>
/// This builder preserves the original format when constructing URLs to maintain
/// consistency with user expectations and prevent broken links.
/// </remarks>
public static class AzureUrlBuilder
{
    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", nameof(AzureUrlBuilder)));
    private static readonly ILogger Log = _logger.Value;

    /// <summary>
    /// Represents the base URL components needed to construct Azure DevOps URLs.
    /// </summary>
    public readonly struct AzureUrlComponents
    {
        /// <summary>
        /// Gets the host type (Modern, Legacy, or NotHosted).
        /// </summary>
        public AzureHostType HostType { get; }

        /// <summary>
        /// Gets the organization name.
        /// </summary>
        public string Organization { get; }

        /// <summary>
        /// Gets the base URL scheme and host (e.g., "https://dev.azure.com" or "https://org.visualstudio.com").
        /// </summary>
        public string BaseUrl { get; }

        /// <summary>
        /// Gets a value indicating whether the components are valid for URL construction.
        /// </summary>
        public bool IsValid => HostType != AzureHostType.Unknown && !string.IsNullOrEmpty(Organization);

        public AzureUrlComponents(AzureHostType hostType, string organization, string baseUrl)
        {
            HostType = hostType;
            Organization = organization;
            BaseUrl = baseUrl;
        }
    }

    /// <summary>
    /// Extracts URL components from an Azure DevOps API URL.
    /// </summary>
    /// <param name="apiUrl">The API URL from Azure DevOps (typically from REST API responses).</param>
    /// <returns>The extracted URL components, or an invalid component set if parsing fails.</returns>
    public static AzureUrlComponents ExtractComponents(string apiUrl)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            Log.Warning("ExtractComponents called with null or empty URL.");
            return default;
        }

        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var uri))
        {
            Log.Warning("ExtractComponents could not parse URL: {Url}", apiUrl);
            return default;
        }

        return ExtractComponents(uri);
    }

    /// <summary>
    /// Extracts URL components from an Azure DevOps API URI.
    /// </summary>
    /// <param name="uri">The URI from Azure DevOps.</param>
    /// <returns>The extracted URL components, or an invalid component set if parsing fails.</returns>
    public static AzureUrlComponents ExtractComponents(Uri uri)
    {
        if (uri == null)
        {
            Log.Warning("ExtractComponents called with null URI.");
            return default;
        }

        var hostType = DetermineHostType(uri);
        var organization = ExtractOrganization(uri, hostType);
        var baseUrl = BuildBaseUrl(uri, hostType);

        return new AzureUrlComponents(hostType, organization, baseUrl);
    }

    /// <summary>
    /// Extracts URL components from an existing AzureUri instance.
    /// </summary>
    /// <param name="azureUri">The AzureUri instance.</param>
    /// <returns>The extracted URL components.</returns>
    public static AzureUrlComponents ExtractComponents(AzureUri azureUri)
    {
        if (azureUri == null || !azureUri.IsValid)
        {
            Log.Warning("ExtractComponents called with invalid AzureUri.");
            return default;
        }

        var baseUrl = BuildBaseUrl(azureUri.Uri, azureUri.HostType);
        return new AzureUrlComponents(azureUri.HostType, azureUri.Organization, baseUrl);
    }

    /// <summary>
    /// Determines the host type from a URI.
    /// </summary>
    private static AzureHostType DetermineHostType(Uri uri)
    {
        if (uri.Host.EndsWith("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            return AzureHostType.Modern;
        }
        else if (uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            return AzureHostType.Legacy;
        }
        else
        {
            // On-premises or other hosted solutions
            return AzureHostType.NotHosted;
        }
    }

    /// <summary>
    /// Extracts the organization name based on the host type.
    /// </summary>
    private static string ExtractOrganization(Uri uri, AzureHostType hostType)
    {
        try
        {
            return hostType switch
            {
                AzureHostType.Modern =>
                    uri.Segments.Length >= 2
                        ? uri.Segments[1].TrimEnd('/')
                        : string.Empty,

                AzureHostType.Legacy =>
                    uri.Host.EndsWith(".vssps.visualstudio.com", StringComparison.OrdinalIgnoreCase)
                        ? uri.Host.Replace(".vssps.visualstudio.com", string.Empty, StringComparison.OrdinalIgnoreCase)
                        : uri.Host.Replace(".visualstudio.com", string.Empty, StringComparison.OrdinalIgnoreCase),

                AzureHostType.NotHosted =>

                    // For on-prem, we might get the organization from the first segment
                    uri.Segments.Length >= 2
                        ? uri.Segments[1].TrimEnd('/')
                        : uri.Host,

                _ => string.Empty,
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to extract organization from URI: {Uri}", uri);
            return string.Empty;
        }
    }

    /// <summary>
    /// Builds the base URL for constructing Azure DevOps URLs.
    /// </summary>
    private static string BuildBaseUrl(Uri uri, AzureHostType hostType)
    {
        return hostType switch
        {
            AzureHostType.Legacy => $"{uri.Scheme}://{uri.Host}",
            AzureHostType.Modern => $"{uri.Scheme}://{uri.Host}",
            AzureHostType.NotHosted => $"{uri.Scheme}://{uri.Host}",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Builds an HTML URL for a build result page.
    /// </summary>
    /// <param name="apiUrl">The API URL from the build object.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="buildId">The build ID.</param>
    /// <returns>The HTML URL for viewing the build results.</returns>
    /// <exception cref="ArgumentException">Thrown when the API URL is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when URL construction fails.</exception>
    public static string BuildBuildResultsUrl(string apiUrl, string projectName, long buildId)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            throw new ArgumentException("URL cannot be null or empty.", nameof(apiUrl));
        }

        var components = ExtractComponents(apiUrl);
        if (!components.IsValid)
        {
            throw new InvalidOperationException($"Could not extract valid components from URL: {apiUrl}");
        }

        return BuildBuildResultsUrl(components, projectName, buildId);
    }

    /// <summary>
    /// Builds an HTML URL for a build result page using pre-extracted components.
    /// </summary>
    /// <param name="components">The URL components.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="buildId">The build ID.</param>
    /// <returns>The HTML URL for viewing the build results.</returns>
    public static string BuildBuildResultsUrl(AzureUrlComponents components, string projectName, long buildId)
    {
        return components.HostType switch
        {
            AzureHostType.Legacy =>
                $"{components.BaseUrl}/{projectName}/_build/results?buildId={buildId}&view=results",

            AzureHostType.Modern =>
                $"{components.BaseUrl}/{components.Organization}/{projectName}/_build/results?buildId={buildId}&view=results",

            AzureHostType.NotHosted =>

                // On-prem follows the same pattern as legacy
                $"{components.BaseUrl}/{projectName}/_build/results?buildId={buildId}&view=results",

            _ => throw new InvalidOperationException($"Unsupported host type: {components.HostType}"),
        };
    }

    /// <summary>
    /// Builds an HTML URL for a build definition page.
    /// </summary>
    /// <param name="apiUrl">The API URL from the definition object.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="definitionId">The definition ID.</param>
    /// <returns>The HTML URL for viewing the build definition.</returns>
    /// <exception cref="ArgumentException">Thrown when the API URL is null or empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown when URL construction fails.</exception>
    public static string BuildDefinitionUrl(string apiUrl, string projectName, long definitionId)
    {
        if (string.IsNullOrWhiteSpace(apiUrl))
        {
            throw new ArgumentException("URL cannot be null or empty.", nameof(apiUrl));
        }

        var components = ExtractComponents(apiUrl);
        if (!components.IsValid)
        {
            throw new InvalidOperationException($"Could not extract valid components from URL: {apiUrl}");
        }

        return BuildDefinitionUrl(components, projectName, definitionId);
    }

    /// <summary>
    /// Builds an HTML URL for a build definition page using pre-extracted components.
    /// </summary>
    /// <param name="components">The URL components.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="definitionId">The definition ID.</param>
    /// <returns>The HTML URL for viewing the build definition.</returns>
    public static string BuildDefinitionUrl(AzureUrlComponents components, string projectName, long definitionId)
    {
        return components.HostType switch
        {
            AzureHostType.Legacy =>
                $"{components.BaseUrl}/{projectName}/_build?definitionId={definitionId}",

            AzureHostType.Modern =>
                $"{components.BaseUrl}/{components.Organization}/{projectName}/_build?definitionId={definitionId}",

            AzureHostType.NotHosted =>

                // On-prem follows the same pattern as legacy
                $"{components.BaseUrl}/{projectName}/_build?definitionId={definitionId}",

            _ => throw new InvalidOperationException($"Unsupported host type: {components.HostType}"),
        };
    }

    /// <summary>
    /// Builds an HTML URL for a pull request page.
    /// </summary>
    /// <param name="repositoryCloneUrl">The repository clone URL.</param>
    /// <param name="pullRequestId">The pull request ID.</param>
    /// <returns>The HTML URL for viewing the pull request.</returns>
    public static string BuildPullRequestUrl(string repositoryCloneUrl, int pullRequestId)
    {
        if (string.IsNullOrWhiteSpace(repositoryCloneUrl))
        {
            throw new ArgumentException("Repository clone URL cannot be null or empty.", nameof(repositoryCloneUrl));
        }

        // The clone URL already contains the repository path, so we just append the PR path
        var baseUrl = repositoryCloneUrl.TrimEnd('/');
        return $"{baseUrl}/pullrequest/{pullRequestId}";
    }

    /// <summary>
    /// Builds an HTML URL for a work item page.
    /// </summary>
    /// <param name="components">The URL components.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="workItemId">The work item ID.</param>
    /// <returns>The HTML URL for viewing the work item.</returns>
    public static string BuildWorkItemUrl(AzureUrlComponents components, string projectName, int workItemId)
    {
        return components.HostType switch
        {
            AzureHostType.Legacy =>
                $"{components.BaseUrl}/{projectName}/_workitems/edit/{workItemId}",

            AzureHostType.Modern =>
                $"{components.BaseUrl}/{components.Organization}/{projectName}/_workitems/edit/{workItemId}",

            AzureHostType.NotHosted =>
                $"{components.BaseUrl}/{projectName}/_workitems/edit/{workItemId}",

            _ => throw new InvalidOperationException($"Unsupported host type: {components.HostType}"),
        };
    }

    /// <summary>
    /// Builds an HTML URL for a query page.
    /// </summary>
    /// <param name="components">The URL components.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="queryId">The query GUID.</param>
    /// <returns>The HTML URL for viewing the query.</returns>
    public static string BuildQueryUrl(AzureUrlComponents components, string projectName, string queryId)
    {
        return components.HostType switch
        {
            AzureHostType.Legacy =>
                $"{components.BaseUrl}/{projectName}/_queries/query/{queryId}",

            AzureHostType.Modern =>
                $"{components.BaseUrl}/{components.Organization}/{projectName}/_queries/query/{queryId}",

            AzureHostType.NotHosted =>
                $"{components.BaseUrl}/{projectName}/_queries/query/{queryId}",

            _ => throw new InvalidOperationException($"Unsupported host type: {components.HostType}"),
        };
    }

    /// <summary>
    /// Builds an HTML URL for a repository page.
    /// </summary>
    /// <param name="components">The URL components.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="repositoryName">The repository name.</param>
    /// <returns>The HTML URL for viewing the repository.</returns>
    public static string BuildRepositoryUrl(AzureUrlComponents components, string projectName, string repositoryName)
    {
        return components.HostType switch
        {
            AzureHostType.Legacy =>
                $"{components.BaseUrl}/{projectName}/_git/{repositoryName}",

            AzureHostType.Modern =>
                $"{components.BaseUrl}/{components.Organization}/{projectName}/_git/{repositoryName}",

            AzureHostType.NotHosted =>
                $"{components.BaseUrl}/{projectName}/_git/{repositoryName}",

            _ => throw new InvalidOperationException($"Unsupported host type: {components.HostType}"),
        };
    }
}
