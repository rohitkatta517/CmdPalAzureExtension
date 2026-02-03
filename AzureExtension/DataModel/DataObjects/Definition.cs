// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Client;
using AzureExtension.Controls;
using AzureExtension.Data;
using AzureExtension.Helpers;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.TeamFoundation.Build.WebApi;
using Serilog;

namespace AzureExtension.DataModel;

/// <summary>
/// Represents a build definition (pipeline) in Azure DevOps.
/// </summary>
[Table("Definition")]
public class Definition : IDefinition
{
    private static readonly ILogger _log = Log.ForContext("SourceContext", $"DataModel/{nameof(Definition)}");

    // This is the time between seeing a potential updated Definition record and updating it.
    // This value / 2 is the average time between Definition updating their Definition data and
    // having it reflected in the datastore.
    private static readonly long _updateThreshold = TimeSpan.FromHours(4).Ticks;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public long InternalId { get; set; } = DataStore.NoForeignKey;

    public string Name { get; set; } = string.Empty;

    public long ProjectId { get; set; } = DataStore.NoForeignKey;

    public long CreationDate { get; set; } = DataStore.NoForeignKey;

    public string HtmlUrl { get; set; } = string.Empty;

    public long TimeUpdated { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    [Computed]
    public Project Project => Project.Get(DataStore, ProjectId);

    [Write(false)]
    [Computed]
    public IBuild? MostRecentBuild => Build.GetForDefinition(DataStore, Id).FirstOrDefault();

    [Write(false)]
    [Computed]
    public DateTime UpdatedAt => TimeUpdated.ToDateTime();

    /// <summary>
    /// Creates a new Definition instance from a DefinitionReference.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="definitionReference">The definition reference from Azure DevOps API.</param>
    /// <param name="projectId">The project ID this definition belongs to.</param>
    /// <returns>A new Definition instance.</returns>
    private static Definition Create(
        DataStore dataStore,
        DefinitionReference definitionReference,
        long projectId)
    {
        var definition = new Definition
        {
            InternalId = definitionReference.Id,
            Name = definitionReference.Name,
            ProjectId = projectId,
            CreationDate = definitionReference.CreatedDate.ToDataStoreInteger(),
            HtmlUrl = CreateDefinitionHtmlUrl(definitionReference.Url, definitionReference.Project.Name, definitionReference.Id),
            TimeUpdated = DateTime.UtcNow.ToDataStoreInteger(),
        };
        definition.DataStore = dataStore;
        return definition;
    }

    /// <summary>
    /// Retrieves a definition by its internal Azure DevOps ID.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="internalId">The internal Azure DevOps definition ID.</param>
    /// <returns>The definition if found; otherwise, null.</returns>
    public static Definition? GetByInternalId(DataStore dataStore, long internalId)
    {
        var sql = @"SELECT * FROM Definition WHERE InternalId = @InternalId";
        var param = new
        {
            InternalId = internalId,
        };

        _log.Debug(DataStore.GetSqlLogMessage(sql, param));
        var definition = dataStore.Connection!.QuerySingleOrDefault<Definition>(sql, param, null);

        if (definition != null)
        {
            definition.DataStore = dataStore;
        }

        return definition;
    }

    /// <summary>
    /// Adds a new definition or updates an existing one based on the internal ID.
    /// Updates are only performed if the time threshold has been exceeded to avoid
    /// unnecessary database operations for data that rarely changes.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="definition">The definition to add or update.</param>
    /// <returns>The definition instance after the operation.</returns>
    public static Definition AddOrUpdate(DataStore dataStore, Definition definition)
    {
        var existingDefinition = GetByInternalId(dataStore, definition.InternalId);
        if (existingDefinition != null)
        {
            // Many of the same Definition records will be created on a sync, and to
            // avoid unnecessary updating and database operations for data that
            // is extremely unlikely to have changed in any significant way, we
            // will only update every UpdateThreshold amount of time.
            if ((definition.TimeUpdated - existingDefinition.TimeUpdated) > _updateThreshold)
            {
                definition.Id = existingDefinition.Id;
                dataStore.Connection!.Update(definition);
                definition.DataStore = dataStore;
                return definition;
            }
            else
            {
                return existingDefinition;
            }
        }

        // No existing definition, add it.
        definition.Id = dataStore.Connection!.Insert(definition);
        definition.DataStore = dataStore;
        return definition;
    }

    /// <summary>
    /// Gets an existing definition or creates a new one from a DefinitionReference.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="definitionReference">The definition reference from Azure DevOps API.</param>
    /// <param name="projectId">The project ID this definition belongs to.</param>
    /// <returns>The definition instance.</returns>
    public static Definition GetOrCreate(
        DataStore dataStore,
        DefinitionReference definitionReference,
        long projectId)
    {
        var definition = Create(dataStore, definitionReference, projectId);
        return AddOrUpdate(dataStore, definition);
    }

    /// <summary>
    /// Retrieves all definitions for a specific project.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="projectId">The project ID to retrieve definitions for.</param>
    /// <returns>An enumerable collection of definitions.</returns>
    public static IEnumerable<Definition> GetAll(DataStore dataStore, long projectId)
    {
        var sql = @"SELECT * FROM Definition WHERE ProjectId = @ProjectId";
        var param = new
        {
            ProjectId = projectId,
        };

        _log.Debug(DataStore.GetSqlLogMessage(sql, param));
        var definitions = dataStore.Connection!.Query<Definition>(sql, param, null);
        foreach (var definition in definitions)
        {
            definition.DataStore = dataStore;
        }

        return definitions;
    }

    /// <summary>
    /// Deletes definitions that are not referenced by any builds.
    /// This is used for cleanup to remove orphaned definition records.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    public static void DeleteUnreferenced(DataStore dataStore)
    {
        var sql = @"DELETE FROM Definition WHERE Id NOT IN (SELECT DISTINCT DefinitionId FROM Build)";
        var command = dataStore.Connection!.CreateCommand();
        command.CommandText = sql;
        _log.Debug(DataStore.GetCommandLogMessage(sql, command));
        var rowsDeleted = command.ExecuteNonQuery();
        _log.Debug(DataStore.GetDeletedLogMessage(rowsDeleted));
    }

    /// <summary>
    /// Creates an HTML URL for a build definition page.
    /// Supports both modern (dev.azure.com) and legacy (visualstudio.com) URL formats.
    /// </summary>
    /// <param name="url">The API URL from the definition object.</param>
    /// <param name="projectName">The project name.</param>
    /// <param name="definitionId">The definition ID.</param>
    /// <returns>The HTML URL for viewing the build definition.</returns>
    private static string CreateDefinitionHtmlUrl(string url, string projectName, long definitionId)
    {
        return AzureUrlBuilder.BuildDefinitionUrl(url, projectName, definitionId);
    }
}
