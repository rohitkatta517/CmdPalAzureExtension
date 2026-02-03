// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Client;
using AzureExtension.Controls;
using AzureExtension.Data;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Serilog;

namespace AzureExtension.DataModel;

/// <summary>
/// Represents a pull request in Azure DevOps.
/// </summary>
[Table("PullRequest")]
public class PullRequest : IPullRequest
{
    private static readonly ILogger _log = Log.ForContext("SourceContext", $"DataModel/{nameof(PullRequest)}");

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public long InternalId { get; set; } = DataStore.NoForeignKey;

    public string Title { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public long RepositoryId { get; set; } = DataStore.NoForeignKey;

    public long CreatorId { get; set; } = DataStore.NoForeignKey;

    public string Status { get; set; } = string.Empty;

    public string PolicyStatus { get; set; } = string.Empty;

    public string PolicyStatusReason { get; set; } = string.Empty;

    public string TargetBranch { get; set; } = string.Empty;

    public long CreationDate { get; set; } = DataStore.NoForeignKey;

    public string HtmlUrl { get; set; } = string.Empty;

    [Write(false)]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    public string RepositoryGuid => Repository.Get(DataStore, RepositoryId).InternalId;

    [Write(false)]
    public Identity? Creator => Identity.Get(DataStore, CreatorId);

    /// <summary>
    /// Creates a new PullRequest instance from a GitPullRequest object.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="gitPullRequest">The pull request object from Azure DevOps API.</param>
    /// <param name="repositoryId">The repository ID this pull request belongs to.</param>
    /// <param name="creatorId">The creator's identity ID.</param>
    /// <param name="status">The policy status.</param>
    /// <param name="statusReason">The policy status reason.</param>
    /// <returns>A new PullRequest instance.</returns>
    private static PullRequest Create(
        DataStore dataStore,
        GitPullRequest gitPullRequest,
        long repositoryId,
        long creatorId,
        PolicyStatus status,
        string statusReason)
    {
        var pullRequest = new PullRequest
        {
            InternalId = gitPullRequest.PullRequestId,
            RepositoryId = repositoryId,
            CreatorId = creatorId,
            Title = gitPullRequest.Title,
            Url = gitPullRequest.Url,
            PolicyStatus = status.ToString(),
            PolicyStatusReason = statusReason,
            TargetBranch = gitPullRequest.TargetRefName,
            CreationDate = gitPullRequest.CreationDate.Ticks,
        };

        var repository = Repository.Get(dataStore, repositoryId);

        // Url in the GitPullRequest object is a REST Api Url, and the links lack an html Url, so we must build it.
        pullRequest.HtmlUrl = AzureUrlBuilder.BuildPullRequestUrl(
            repository.CloneUrl,
            gitPullRequest.PullRequestId);

        pullRequest.Id = dataStore.Connection!.Insert(pullRequest);
        return pullRequest;
    }

    /// <summary>
    /// Retrieves a pull request by its internal Azure DevOps ID.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="internalId">The internal Azure DevOps pull request ID.</param>
    /// <returns>The pull request if found; otherwise, null.</returns>
    public static PullRequest? GetByInternalId(DataStore dataStore, long internalId)
    {
        var sql = @"SELECT * FROM PullRequest WHERE InternalId = @InternalId";
        var param = new
        {
            InternalId = internalId,
        };

        _log.Debug(DataStore.GetSqlLogMessage(sql, param));
        var pullRequest = dataStore.Connection!.QueryFirstOrDefault<PullRequest>(sql, param, null);

        if (pullRequest != null)
        {
            pullRequest.DataStore = dataStore;
        }

        return pullRequest;
    }

    /// <summary>
    /// Adds a new pull request or updates an existing one based on the internal ID.
    /// Pull requests are always updated if they exist to reflect the latest status.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="pullRequest">The pull request to add or update.</param>
    /// <returns>The pull request instance after the operation.</returns>
    public static PullRequest AddOrUpdate(DataStore dataStore, PullRequest pullRequest)
    {
        var existingPullRequest = GetByInternalId(dataStore, pullRequest.InternalId);
        if (existingPullRequest != null)
        {
            pullRequest.Id = existingPullRequest.Id;
            dataStore.Connection!.Update(pullRequest);
            pullRequest.DataStore = dataStore;
            return pullRequest;
        }

        pullRequest.DataStore = dataStore;
        pullRequest.Id = dataStore.Connection!.Insert(pullRequest);
        return pullRequest;
    }

    /// <summary>
    /// Gets an existing pull request or creates a new one from a GitPullRequest object.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="gitPullRequest">The pull request object from Azure DevOps API.</param>
    /// <param name="repositoryId">The repository ID this pull request belongs to.</param>
    /// <param name="creatorId">The creator's identity ID.</param>
    /// <param name="status">The policy status.</param>
    /// <param name="statusReason">The policy status reason.</param>
    /// <returns>The pull request instance.</returns>
    public static PullRequest GetOrCreate(
        DataStore dataStore,
        GitPullRequest gitPullRequest,
        long repositoryId,
        long creatorId,
        PolicyStatus status,
        string statusReason)
    {
        var pullRequest = Create(dataStore, gitPullRequest, repositoryId, creatorId, status, statusReason);
        return AddOrUpdate(dataStore, pullRequest);
    }

    /// <summary>
    /// Retrieves all pull requests associated with a specific pull request search, ordered by creation date.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="pullRequestSearch">The pull request search to retrieve pull requests for.</param>
    /// <returns>An enumerable collection of pull requests.</returns>
    public static IEnumerable<PullRequest> GetForPullRequestSearch(DataStore dataStore, PullRequestSearch pullRequestSearch)
    {
        var sql = @"SELECT PR.* FROM PullRequest PR 
                    INNER JOIN PullRequestSearchPullRequest PRSP ON PR.Id = PRSP.PullRequest 
                    WHERE PRSP.PullRequestSearch = @PullRequestSearchId 
                    ORDER BY PR.CreationDate DESC, PRSP.TimeUpdated DESC";
        var param = new
        {
            PullRequestSearchId = pullRequestSearch.Id,
        };

        _log.Debug(DataStore.GetSqlLogMessage(sql, param));
        var pullRequests = dataStore.Connection!.Query<PullRequest>(sql, param, null);
        foreach (var pullRequest in pullRequests)
        {
            pullRequest.DataStore = dataStore;
        }

        return pullRequests;
    }

    /// <summary>
    /// Deletes all pull requests that are not referenced by any pull request search.
    /// This is used for cleanup to remove orphaned pull request records.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    public static void DeleteNotReferencedBySearch(DataStore dataStore)
    {
        var sql = @"DELETE FROM PullRequest WHERE Id NOT IN (SELECT PullRequest FROM PullRequestSearchPullRequest)";
        _log.Debug(DataStore.GetSqlLogMessage(sql));
        var rowsDeleted = dataStore.Connection!.Execute(sql);
        _log.Debug(DataStore.GetDeletedLogMessage(rowsDeleted));
    }
}
