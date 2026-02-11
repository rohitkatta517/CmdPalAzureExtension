// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Controls;
using AzureExtension.Data;
using Serilog;

namespace AzureExtension.PersistentData;

public class ProjectSettingsRepository : ISavedSearchesProvider<IMyWorkItemsSearch>
{
    private static readonly Lazy<ILogger> _logger = new(() => Log.ForContext("SourceContext", nameof(ProjectSettingsRepository)));

    private static readonly ILogger _log = _logger.Value;

    private readonly DataStore _dataStore;

    private void ValidateDataStore()
    {
        if (_dataStore == null || !_dataStore.IsConnected)
        {
            throw new DataStoreInaccessibleException("Persistent DataStore is not available.");
        }
    }

    public ProjectSettingsRepository(DataStore dataStore)
    {
        _dataStore = dataStore;
    }

    public void RemoveSavedSearch(IMyWorkItemsSearch search)
    {
        ValidateDataStore();

        _log.Information($"Removing project setting: {search.OrganizationUrl} - {search.ProjectName}.");
        if (ProjectSettings.Get(_dataStore, search.OrganizationUrl, search.ProjectName) == null)
        {
            throw new InvalidOperationException($"Project setting {search.OrganizationUrl} - {search.ProjectName} not found.");
        }

        ProjectSettings.Remove(_dataStore, search.OrganizationUrl, search.ProjectName);
    }

    public IEnumerable<IMyWorkItemsSearch> GetSavedSearches(bool getTopLevelOnly = false)
    {
        ValidateDataStore();
        return ProjectSettings.GetAll(_dataStore);
    }

    public void AddOrUpdateSearch(IMyWorkItemsSearch search)
    {
        ValidateDataStore();
        ProjectSettings.AddOrUpdate(_dataStore, search.OrganizationUrl, search.ProjectName);
    }
}
