// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Client;
using AzureExtension.Controls;
using AzureExtension.Data;
using AzureExtension.Helpers;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.VisualStudio.Services.Common;
using Serilog;
using TFModels = Microsoft.TeamFoundation.Build.WebApi;

namespace AzureExtension.DataModel;

/// <summary>
/// Represents a build execution in Azure DevOps.
/// </summary>
[Table("Build")]
public class Build : IBuild
{
    private static readonly ILogger _log = Log.ForContext("SourceContext", $"DataModel/{nameof(Build)}");

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public long InternalId { get; set; } = DataStore.NoForeignKey;

    public string BuildNumber { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public long QueueTime { get; set; } = DataStore.NoForeignKey;

    public long StartTime { get; set; } = DataStore.NoForeignKey;

    public long FinishTime { get; set; } = DataStore.NoForeignKey;

    public string Url { get; set; } = string.Empty;

    public long DefinitionId { get; set; } = DataStore.NoForeignKey;

    public string SourceBranch { get; set; } = string.Empty;

    public string TriggerMessage { get; set; } = string.Empty;

    public long RequesterId { get; set; } = DataStore.NoForeignKey;

    public long TimeUpdated { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    [Computed]
    public Identity? Requester => Identity.Get(DataStore, RequesterId);

    /// <summary>
    /// Creates a new Build instance from a TeamFoundation Build object.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="tfBuild">The build object from Azure DevOps API.</param>
    /// <param name="definitionId">The definition ID this build belongs to.</param>
    /// <param name="requesterId">The requester's identity ID.</param>
    /// <returns>A new Build instance.</returns>
    private static Build Create(DataStore dataStore, TFModels.Build tfBuild, long definitionId, long requesterId)
    {
        var build = new Build
        {
            InternalId = tfBuild.Id,
            BuildNumber = tfBuild.BuildNumber,
            Status = tfBuild.Status.ToString() ?? string.Empty,
            Result = tfBuild.Result.ToString() ?? string.Empty,
            QueueTime = tfBuild.QueueTime?.ToDataStoreInteger() ?? DataStore.NoForeignKey,
            StartTime = tfBuild.StartTime?.ToDataStoreInteger() ?? DataStore.NoForeignKey,
            FinishTime = tfBuild.FinishTime?.ToDataStoreInteger() ?? DataStore.NoForeignKey,
            Url = ConvertBuildUrlToHtmlUrl(tfBuild.Url, tfBuild.Project.Name, tfBuild.Id),
            DefinitionId = definitionId,
            SourceBranch = tfBuild.SourceBranch,
            TriggerMessage = tfBuild.TriggerInfo.GetValueOrDefault("ci.message", string.Empty),
            RequesterId = requesterId,
            TimeUpdated = DateTime.UtcNow.ToDataStoreInteger(),
        };

        return build;
    }

    /// <summary>
    /// Retrieves a build by its internal Azure DevOps ID.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="internalId">The internal Azure DevOps build ID.</param>
    /// <returns>The build if found; otherwise, null.</returns>
    public static Build? GetByInternalId(DataStore dataStore, long internalId)
    {
        var sql = @"SELECT * FROM Build WHERE InternalId = @InternalId";
        var param = new
        {
            InternalId = internalId,
        };

        _log.Debug(DataStore.GetSqlLogMessage(sql, param));
        var build = dataStore.Connection!.QueryFirstOrDefault<Build>(sql, param, null);

        if (build != null)
        {
            build.DataStore = dataStore;
        }

        return build;
    }

    /// <summary>
    /// Adds a new build or updates an existing one based on the internal ID.
    /// Builds are always updated if they exist to reflect the latest status.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="build">The build to add or update.</param>
    /// <returns>The build instance after the operation.</returns>
    private static Build AddOrUpdate(DataStore dataStore, Build build)
    {
        var existingBuild = GetByInternalId(dataStore, build.InternalId);
        if (existingBuild != null)
        {
            build.Id = existingBuild.Id;
            dataStore.Connection!.Update(build);
            build.DataStore = dataStore;
            return build;
        }

        build.Id = dataStore.Connection!.Insert(build);
        build.DataStore = dataStore;
        return build;
    }

    /// <summary>
    /// Gets an existing build or creates a new one from a TeamFoundation Build object.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="tfBuild">The build object from Azure DevOps API.</param>
    /// <param name="definitionId">The definition ID this build belongs to.</param>
    /// <param name="requesterId">The requester's identity ID.</param>
    /// <returns>The build instance.</returns>
    public static Build GetOrCreate(DataStore dataStore, TFModels.Build tfBuild, long definitionId, long requesterId)
    {
        var newBuild = Create(dataStore, tfBuild, definitionId, requesterId);
        return AddOrUpdate(dataStore, newBuild);
    }

    /// <summary>
    /// Retrieves all builds for a specific definition, ordered by update time.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="definitionId">The definition ID to retrieve builds for.</param>
    /// <returns>An enumerable collection of builds, or an empty collection if dataStore is null.</returns>
    public static IEnumerable<Build> GetForDefinition(DataStore? dataStore, long definitionId)
    {
        if (dataStore == null)
        {
            return [];
        }

        var sql = @"SELECT * FROM Build WHERE DefinitionId = @DefinitionId ORDER BY TimeUpdated ASC";
        var param = new
        {
            DefinitionId = definitionId,
        };

        _log.Debug(DataStore.GetSqlLogMessage(sql, param));
        var builds = dataStore.Connection!.Query<Build>(sql, param, null);
        foreach (var build in builds)
        {
            build.DataStore = dataStore;
        }

        return builds;
    }

    /// <summary>
    /// Deletes all builds that were updated before the specified date.
    /// This is used for cleanup to remove old build records.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="date">The cutoff date; builds updated before this date will be deleted.</param>
    public static void DeleteBefore(DataStore dataStore, DateTime date)
    {
        var sql = @"DELETE FROM Build WHERE TimeUpdated < $Time";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$Time", date.ToDataStoreInteger());
        _log.Debug(DataStore.GetCommandLogMessage(sql, command));
        var rowsDeleted = command.ExecuteNonQuery();
        _log.Debug(DataStore.GetDeletedLogMessage(rowsDeleted));
    }

    /// <summary>
    /// Converts an Azure DevOps API URL to a human-readable HTML URL for the build results page.
    /// Supports both modern (dev.azure.com) and legacy (visualstudio.com) URL formats.
    /// </summary>
    /// <param name="url">The API URL from the build object.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="buildId">The build ID.</param>
    /// <returns>The HTML URL for viewing the build results.</returns>
    public static string ConvertBuildUrlToHtmlUrl(string url, string projectName, long buildId)
    {
        return AzureUrlBuilder.BuildBuildResultsUrl(url, projectName, buildId);
    }
}
