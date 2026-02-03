// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Client;
using AzureExtension.Controls;
using AzureExtension.Data;
using AzureExtension.Helpers;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.VisualStudio.Services.WebApi;
using Serilog;
using TFModels = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AzureExtension.DataModel;

/// <summary>
/// Represents a work item in Azure DevOps.
/// </summary>
[Table("WorkItem")]
public class WorkItem : IWorkItem
{
    private static readonly ILogger _log = Log.ForContext("SourceContext", $"DataModel/{nameof(WorkItem)}");

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public long InternalId { get; set; } = DataStore.NoForeignKey;

    public string SystemTitle { get; set; } = string.Empty;

    public string HtmlUrl { get; set; } = string.Empty;

    public string SystemState { get; set; } = string.Empty;

    public string SystemReason { get; set; } = string.Empty;

    public long SystemAssignedToId { get; set; } = DataStore.NoForeignKey;

    public long SystemCreatedDate { get; set; } = DataStore.NoForeignKey;

    public long SystemCreatedById { get; set; } = DataStore.NoForeignKey;

    public long SystemChangedDate { get; set; } = DataStore.NoForeignKey;

    public long SystemChangedById { get; set; } = DataStore.NoForeignKey;

    public long SystemWorkItemTypeId { get; set; } = DataStore.NoForeignKey;

    [Write(false)]
    private DataStore? DataStore { get; set; }

    [Write(false)]
    public Identity? SystemAssignedTo => Identity.Get(DataStore, SystemAssignedToId);

    [Write(false)]
    public Identity? SystemCreatedBy => Identity.Get(DataStore, SystemCreatedById);

    [Write(false)]
    public Identity? SystemChangedBy => Identity.Get(DataStore, SystemChangedById);

    [Write(false)]
    public WorkItemType? SystemWorkItemType => WorkItemType.Get(DataStore, SystemWorkItemTypeId);

    [Write(false)]
    public string WorkItemTypeName => SystemWorkItemType?.Name ?? string.Empty;

    public static readonly string IdentityRefFieldValueName = "Microsoft.VisualStudio.Services.WebApi.IdentityRef";
    public static readonly string SystemIdFieldName = "System.Id";
    public static readonly string WorkItemHtmlUrlFieldName = "DevHome.AzureExtension.WorkItemHtmlUrl";
    public static readonly string WorkItemTypeFieldName = "System.WorkItemType";

    private static readonly List<string> _fields =
    [

        // "System.Id" is implicitly added.
        "System.State",
        "System.Reason",
        "System.AssignedTo",
        "System.CreatedDate",
        "System.CreatedBy",
        "System.ChangedDate",
        "System.ChangedBy",
        "System.Title",
        "System.WorkItemType",
    ];

    /// <summary>
    /// Creates a new WorkItem instance from a TeamFoundation WorkItem object.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="tfWorkItem">The work item object from Azure DevOps API.</param>
    /// <param name="connection">The VSS connection for additional API calls.</param>
    /// <param name="dataProvider">The data provider for retrieving additional data.</param>
    /// <param name="projectId">The project ID this work item belongs to.</param>
    /// <param name="tfWorkItemType">Optional work item type information.</param>
    /// <returns>A new WorkItem instance.</returns>
    private static WorkItem Create(
        DataStore dataStore,
        TFModels.WorkItem tfWorkItem,
        IVssConnection connection,
        IAzureLiveDataProvider dataProvider,
        long projectId,
        TFModels.WorkItemType? tfWorkItemType = null)
    {
        var workItem = new WorkItem();

        if (tfWorkItem.Id != null)
        {
            workItem.InternalId = tfWorkItem.Id.Value;
        }

        var htmlUrl = Links.GetLinkHref(tfWorkItem.Links, "html");
        workItem.HtmlUrl = htmlUrl ?? string.Empty;

        foreach (var field in _fields)
        {
            if (!tfWorkItem.Fields.ContainsKey(field))
            {
                continue;
            }

            var fieldValue = tfWorkItem.Fields[field].ToString();
            if (fieldValue is null)
            {
                continue;
            }

            if (tfWorkItem.Fields[field] is DateTime dateTime)
            {
                if (field == "System.CreatedDate")
                {
                    workItem.SystemCreatedDate = dateTime.Ticks;
                }
                else if (field == "System.ChangedDate")
                {
                    workItem.SystemChangedDate = dateTime.Ticks;
                }

                continue;
            }

            var fieldIdentityRef = tfWorkItem.Fields[field] as IdentityRef;
            if (fieldValue == IdentityRefFieldValueName && fieldIdentityRef != null)
            {
                var identity = Identity.GetOrCreateIdentity(dataStore, fieldIdentityRef, connection, dataProvider);

                if (field == "System.CreatedBy")
                {
                    workItem.SystemCreatedById = identity.Id;
                }
                else if (field == "System.ChangedBy")
                {
                    workItem.SystemCreatedById = identity.Id;
                }
                else if (field == "System.AssignedTo")
                {
                    workItem.SystemAssignedToId = identity.Id;
                }

                continue;
            }

            if (field == WorkItemTypeFieldName && tfWorkItemType != null)
            {
                // Need a separate query to create WorkItemType object.
                var workItemType = WorkItemType.GetOrCreateByTeamWorkItemType(dataStore, tfWorkItemType, projectId);

                workItem.SystemWorkItemTypeId = workItemType.Id;
                continue;
            }

            if (field == "System.State")
            {
                workItem.SystemState = fieldValue;
                continue;
            }

            if (field == "System.Reason")
            {
                workItem.SystemReason = fieldValue;
                continue;
            }

            if (field == "System.Title")
            {
                workItem.SystemTitle = fieldValue;
                continue;
            }
        }

        return workItem;
    }

    /// <summary>
    /// Retrieves a work item by its internal Azure DevOps ID.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="internalId">The internal Azure DevOps work item ID.</param>
    /// <returns>The work item if found; otherwise, null.</returns>
    public static WorkItem? GetByInternalId(DataStore dataStore, long internalId)
    {
        var sql = @"SELECT * FROM WorkItem WHERE InternalId = @InternalId";
        var param = new
        {
            InternalId = internalId,
        };

        _log.Debug(DataStore.GetSqlLogMessage(sql, param));
        var workItem = dataStore.Connection!.QueryFirstOrDefault<WorkItem>(sql, param, null);

        if (workItem != null)
        {
            workItem.DataStore = dataStore;
        }

        return workItem;
    }

    /// <summary>
    /// Adds a new work item or updates an existing one based on the internal ID.
    /// Work items are always updated if they exist to reflect the latest state.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="workItem">The work item to add or update.</param>
    /// <returns>The work item instance after the operation.</returns>
    public static WorkItem AddOrUpdate(DataStore dataStore, WorkItem workItem)
    {
        var existingWorkItem = GetByInternalId(dataStore, workItem.InternalId);
        if (existingWorkItem != null)
        {
            workItem.Id = existingWorkItem.Id;
            dataStore.Connection!.Update(workItem);
            workItem.DataStore = dataStore;
            return workItem;
        }

        // If the work item does not exist, insert it.
        workItem.DataStore = dataStore;
        workItem.Id = dataStore.Connection!.Insert(workItem);
        return workItem;
    }

    /// <summary>
    /// Gets an existing work item or creates a new one from a TeamFoundation WorkItem object.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="tfWorkItem">The work item object from Azure DevOps API.</param>
    /// <param name="connection">The VSS connection for additional API calls.</param>
    /// <param name="dataProvider">The data provider for retrieving additional data.</param>
    /// <param name="projectId">The project ID this work item belongs to.</param>
    /// <param name="workItemType">The work item type information.</param>
    /// <returns>The work item instance.</returns>
    public static WorkItem GetOrCreate(
        DataStore dataStore,
        TFModels.WorkItem tfWorkItem,
        IVssConnection connection,
        IAzureLiveDataProvider dataProvider,
        long projectId,
        TFModels.WorkItemType workItemType)
    {
        var newWorkItem = Create(dataStore, tfWorkItem, connection, dataProvider, projectId, workItemType);
        return AddOrUpdate(dataStore, newWorkItem);
    }

    /// <summary>
    /// Retrieves a work item by its database ID.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="workItemId">The database ID of the work item.</param>
    /// <returns>The work item if found; otherwise, null.</returns>
    public static WorkItem? Get(DataStore dataStore, long workItemId)
    {
        var sql = @"SELECT * FROM WorkItem WHERE Id = @Id";
        var param = new
        {
            Id = workItemId,
        };

        _log.Debug(DataStore.GetSqlLogMessage(sql, param));
        var workItem = dataStore.Connection!.QueryFirstOrDefault<WorkItem>(sql, param, null);
        if (workItem != null)
        {
            workItem.DataStore = dataStore;
        }

        return workItem;
    }

    /// <summary>
    /// Retrieves all work items associated with a specific query, ordered by update time.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    /// <param name="query">The query to retrieve work items for.</param>
    /// <returns>An enumerable collection of work items.</returns>
    public static IEnumerable<WorkItem> GetForQuery(DataStore dataStore, Query query)
    {
        var sql = @"SELECT * FROM WorkItem WHERE Id IN (SELECT WorkItem FROM QueryWorkItem WHERE Query = @QueryId ORDER BY TimeUpdated ASC)";
        var param = new
        {
            QueryId = query.Id,
        };

        _log.Debug(DataStore.GetSqlLogMessage(sql, param));
        var workItems = dataStore.Connection!.Query<WorkItem>(sql, param, null) ?? new List<WorkItem>();
        foreach (var workItem in workItems)
        {
            workItem.DataStore = dataStore;
        }

        return workItems;
    }

    /// <summary>
    /// Deletes all work items that are not referenced by any query.
    /// This is used for cleanup to remove orphaned work item records.
    /// </summary>
    /// <param name="dataStore">The data store instance.</param>
    public static void DeleteNotReferencedByQuery(DataStore dataStore)
    {
        var sql = @"DELETE FROM WorkItem WHERE Id NOT IN (SELECT WorkItem FROM QueryWorkItem)";
        _log.Debug(DataStore.GetSqlLogMessage(sql));
        var rowsDeleted = dataStore.Connection!.Execute(sql);
        _log.Debug(DataStore.GetDeletedLogMessage(rowsDeleted));
    }
}
