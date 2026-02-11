// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Data;
using Serilog;

namespace AzureExtension.PersistentData;

public class BoardLinkRepository
{
    private static readonly Lazy<ILogger> _logger = new(() => Log.ForContext("SourceContext", nameof(BoardLinkRepository)));

    private static readonly ILogger _log = _logger.Value;

    private readonly DataStore _dataStore;

    public BoardLinkRepository(DataStore dataStore)
    {
        _dataStore = dataStore;
    }

    private void ValidateDataStore()
    {
        if (_dataStore == null || !_dataStore.IsConnected)
        {
            throw new DataStoreInaccessibleException("Persistent DataStore is not available.");
        }
    }

    public IEnumerable<BoardLink> GetAll()
    {
        ValidateDataStore();
        return BoardLink.GetAll(_dataStore);
    }

    public void AddOrUpdate(string url, string displayName)
    {
        ValidateDataStore();
        BoardLink.AddOrUpdate(_dataStore, url, displayName);
    }

    public void Remove(long id)
    {
        ValidateDataStore();

        var boardLink = BoardLink.Get(_dataStore, id);
        if (boardLink == null)
        {
            throw new InvalidOperationException($"Board link with id {id} not found.");
        }

        _log.Information($"Removing board link: {boardLink.DisplayName}");
        BoardLink.Remove(_dataStore, id);
    }
}
