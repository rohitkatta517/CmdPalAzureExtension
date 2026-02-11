// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Account;
using AzureExtension.Client;
using AzureExtension.Controls;
using AzureExtension.Data;
using AzureExtension.DataModel;
using AzureExtension.PersistentData;
using Serilog;
using Query = AzureExtension.DataModel.Query;
using TFModels = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using WorkItem = AzureExtension.DataModel.WorkItem;

namespace AzureExtension.DataManager;

public class AzureDataMyWorkItemsManager
    : ISearchDataProvider<IMyWorkItemsSearch, Query>, IContentDataProvider<IMyWorkItemsSearch, WorkItem>, ISearchDataProvider, IContentDataProvider, IDataUpdater
{
    private readonly TimeSpan _queryWorkItemDeletionTime = TimeSpan.FromMinutes(2);
    private readonly ILogger _log;
    private readonly DataStore _dataStore;
    private readonly IAccountProvider _accountProvider;
    private readonly IAzureLiveDataProvider _liveDataProvider;
    private readonly IConnectionProvider _connectionProvider;
    private readonly IEnumerable<IAzureSearchRepository> _searchRepositories;
    private readonly ProjectSettingsRepository? _projectSettingsRepository;

    private const int AzureAPIWorkItemLimit = 200;

    public const string MyWorkItemsQueryIdPrefix = "my-work-items:";

    private static readonly string _wiqlTemplate =
        "SELECT [System.Id] FROM WorkItems WHERE [System.AssignedTo] = @Me AND [System.State] <> 'Closed' AND [System.State] <> 'Removed' ORDER BY [System.ChangedDate] DESC";

    public AzureDataMyWorkItemsManager(
        DataStore dataStore,
        IAccountProvider accountProvider,
        IAzureLiveDataProvider liveDataProvider,
        IConnectionProvider connectionProvider,
        IEnumerable<IAzureSearchRepository> searchRepositories,
        ProjectSettingsRepository? projectSettingsRepository = null)
    {
        _dataStore = dataStore;
        _accountProvider = accountProvider;
        _log = Log.ForContext("SourceContext", nameof(AzureDataMyWorkItemsManager));
        _liveDataProvider = liveDataProvider;
        _connectionProvider = connectionProvider;
        _searchRepositories = searchRepositories;
        _projectSettingsRepository = projectSettingsRepository;
    }

    private void ValidateDataStore()
    {
        if (_dataStore == null || !_dataStore.IsConnected)
        {
            throw new DataStoreInaccessibleException("Cache DataStore is not available.");
        }
    }

    public static string GetQueryIdForSearch(IMyWorkItemsSearch search)
    {
        return $"{MyWorkItemsQueryIdPrefix}{search.OrganizationUrl}|{search.ProjectName}";
    }

    public Query? GetDataForSearch(IMyWorkItemsSearch search)
    {
        ValidateDataStore();
        var account = _accountProvider.GetDefaultAccount();
        var queryId = GetQueryIdForSearch(search);
        return Query.Get(_dataStore, queryId, account.Username);
    }

    public bool IsNewOrStale(IMyWorkItemsSearch search, TimeSpan refreshCooldown)
    {
        var dsQuery = GetDataForSearch(search);
        return dsQuery == null || DateTime.UtcNow - dsQuery.UpdatedAt > refreshCooldown;
    }

    public bool IsNewOrStale(DataUpdateParameters parameters, TimeSpan refreshCooldown)
    {
        return IsNewOrStale((parameters.UpdateObject as IMyWorkItemsSearch)!, refreshCooldown);
    }

    private static int GetWorkItemTypePriority(string typeName)
    {
        return typeName switch
        {
            "Bug" => 0,
            "Feature" => 1,
            "Product Backlog Item" => 2,
            "User Story" => 3,
            _ when typeName.Equals("Task", StringComparison.OrdinalIgnoreCase) => 10,
            _ => 5,
        };
    }

    public IEnumerable<WorkItem> GetDataObjects(IMyWorkItemsSearch search)
    {
        ValidateDataStore();
        var dsQuery = GetDataForSearch(search);
        if (dsQuery == null)
        {
            return [];
        }

        return WorkItem.GetForQuery(_dataStore, dsQuery)
            .OrderBy(w => GetWorkItemTypePriority(w.WorkItemTypeName))
            .ThenByDescending(w => w.SystemChangedDate);
    }

    public object? GetDataForSearch(IAzureSearch search)
    {
        return GetDataForSearch(search as IMyWorkItemsSearch ?? throw new InvalidOperationException("Invalid search type"));
    }

    public IEnumerable<object> GetDataObjects(IAzureSearch search)
    {
        return GetDataObjects(search as IMyWorkItemsSearch ?? throw new InvalidOperationException("Invalid search type"));
    }

    public async Task UpdateMyWorkItemsAsync(IMyWorkItemsSearch search, CancellationToken cancellationToken)
    {
        _log.Information("Updating My Work Items for {Project} at {Org}", search.ProjectName, search.OrganizationUrl);

        var account = await _accountProvider.GetDefaultAccountAsync();
        var connectionUri = new Uri(search.OrganizationUrl);
        var vssConnection = await _connectionProvider.GetVssConnectionAsync(connectionUri, account);

        var org = Organization.GetOrCreate(_dataStore, connectionUri);

        var project = Project.Get(_dataStore, search.ProjectName, org.Id);
        if (project is null)
        {
            _log.Information("Project {Project} not cached, fetching from ADO", search.ProjectName);
            var teamProject = await _liveDataProvider.GetTeamProject(vssConnection, search.ProjectName);
            project = Project.GetOrCreateByTeamProject(_dataStore, teamProject, org.Id);
        }

        _log.Information("Running WIQL query for project {Project} (InternalId: {Id})", search.ProjectName, project.InternalId);
        var queryResult = await _liveDataProvider.QueryByWiqlAsync(vssConnection, project.InternalId, _wiqlTemplate, cancellationToken);

        var workItemIds = new List<int>();
        if (queryResult.WorkItems is not null)
        {
            foreach (var item in queryResult.WorkItems)
            {
                if (item is not null)
                {
                    workItemIds.Add(item.Id);
                }
            }
        }

        _log.Information("WIQL returned {Count} work item IDs", workItemIds.Count);

        var workItems = new List<TFModels.WorkItem>();
        if (workItemIds.Count > 0)
        {
            var workItemIdChunks = workItemIds.Chunk(AzureAPIWorkItemLimit);
            var chunkedWorkItemsTasks = new List<Task<List<TFModels.WorkItem>>>();
            foreach (var chunk in workItemIdChunks)
            {
                var chunkedWorkItemsTask = _liveDataProvider.GetWorkItemsAsync(vssConnection, project.InternalId, chunk, TFModels.WorkItemExpand.Links, TFModels.WorkItemErrorPolicy.Omit, cancellationToken);
                chunkedWorkItemsTasks.Add(chunkedWorkItemsTask);
            }

            foreach (var task in chunkedWorkItemsTasks)
            {
                var chunkedWorkItems = await task;
                if (chunkedWorkItems != null && chunkedWorkItems.Count > 0)
                {
                    workItems.AddRange(chunkedWorkItems);
                }
            }
        }

        _log.Information("Fetched {Count} work items, processing types", workItems.Count);

        var queryId = GetQueryIdForSearch(search);
        var dsQuery = Query.GetOrCreate(_dataStore, queryId, project.Id, account.Username, search.Name);

        // Deduplicate work item type lookups — only fetch each unique type once
        var uniqueTypeNames = workItems
            .Select(w => w.Fields["System.WorkItemType"].ToString()!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var typeTasksByName = new Dictionary<string, Task<TFModels.WorkItemType>>(StringComparer.OrdinalIgnoreCase);
        foreach (var typeName in uniqueTypeNames)
        {
            typeTasksByName[typeName] = _liveDataProvider.GetWorkItemTypeAsync(vssConnection, project.InternalId, typeName, cancellationToken);
        }

        await Task.WhenAll(typeTasksByName.Values);

        var typeCache = new Dictionary<string, TFModels.WorkItemType>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in typeTasksByName)
        {
            typeCache[kvp.Key] = kvp.Value.Result;
        }

        foreach (var workItem in workItems)
        {
            var typeName = workItem.Fields["System.WorkItemType"].ToString()!;
            var workItemTypeInfo = typeCache[typeName];

            var cmdPalWorkItem = WorkItem.GetOrCreate(_dataStore, workItem, vssConnection, _liveDataProvider, project.Id, workItemTypeInfo);
            QueryWorkItem.AddWorkItemToQuery(_dataStore, dsQuery.Id, cmdPalWorkItem.Id);
        }

        QueryWorkItem.DeleteBefore(_dataStore, dsQuery, DateTime.UtcNow - _queryWorkItemDeletionTime);
        _log.Information("My Work Items update complete for {Project}: {Count} items", search.ProjectName, workItems.Count);
    }

    public void PruneObsoleteData()
    {
        // Pruning is handled by the main query manager since we share the same tables
    }

    public IEnumerable<IMyWorkItemsSearch> DiscoverSearches()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var searches = new List<IMyWorkItemsSearch>();

        // Primary source: explicitly configured projects
        if (_projectSettingsRepository != null)
        {
            try
            {
                foreach (var projectSearch in _projectSettingsRepository.GetSavedSearches())
                {
                    var key = $"{projectSearch.OrganizationUrl.TrimEnd('/')}|{projectSearch.ProjectName}";
                    if (seen.Add(key))
                    {
                        searches.Add(projectSearch);
                    }
                }
            }
            catch (DataStoreInaccessibleException ex)
            {
                _log.Warning(ex, "Could not read project settings: {Message}", ex.Message);
            }
        }

        // Secondary fallback: infer from saved searches
        foreach (var repo in _searchRepositories)
        {
            foreach (var search in repo.GetAll())
            {
                var azureUri = new AzureUri(search.Url);
                if (!azureUri.IsValid || string.IsNullOrEmpty(azureUri.Organization) || string.IsNullOrEmpty(azureUri.Project))
                {
                    continue;
                }

                var key = $"{azureUri.Connection.ToString().TrimEnd('/')}|{azureUri.Project}";
                if (seen.Add(key))
                {
                    searches.Add(new MyWorkItemsSearch(
                        $"My Work Items - {azureUri.Project}",
                        azureUri.Connection.ToString(),
                        azureUri.Project));
                }
            }
        }

        return searches;
    }

    public async Task UpdateData(DataUpdateParameters parameters)
    {
        if (parameters.UpdateType == DataUpdateType.All)
        {
            var searches = DiscoverSearches();
            foreach (var search in searches)
            {
                await UpdateMyWorkItemsAsync(search, parameters.CancellationToken.GetValueOrDefault());
            }

            return;
        }

        await UpdateMyWorkItemsAsync((parameters.UpdateObject as IMyWorkItemsSearch)!, parameters.CancellationToken.GetValueOrDefault());
    }
}
