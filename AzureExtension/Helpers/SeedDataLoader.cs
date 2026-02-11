// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using AzureExtension.Data;
using AzureExtension.PersistentData;
using Dapper;
using Serilog;

namespace AzureExtension.Helpers;

public static class SeedDataLoader
{
    private static readonly Lazy<ILogger> _logger = new(() => Log.ForContext("SourceContext", nameof(SeedDataLoader)));

    private static readonly ILogger _log = _logger.Value;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static void SeedIfEmpty(DataStore persistentDataStore)
    {
        try
        {
            if (!IsDatabaseEmpty(persistentDataStore))
            {
                _log.Information("Persistent data store already has data, skipping seed.");
                return;
            }

            var seedFilePath = Path.Combine(AppContext.BaseDirectory, "seed-data.json");
            if (!File.Exists(seedFilePath))
            {
                _log.Information("No seed-data.json found at {Path}, skipping seed.", seedFilePath);
                return;
            }

            var json = File.ReadAllText(seedFilePath);
            var seedData = JsonSerializer.Deserialize<SeedData>(json, _jsonOptions);

            if (seedData is null)
            {
                _log.Warning("seed-data.json deserialized to null, skipping seed.");
                return;
            }

            var seededCount = 0;

            foreach (var ps in seedData.ProjectSettings ?? [])
            {
                ProjectSettings.AddOrUpdate(persistentDataStore, ps.OrganizationUrl, ps.ProjectName);
                seededCount++;
            }

            foreach (var q in seedData.Queries ?? [])
            {
                Query.AddOrUpdate(persistentDataStore, q.Name, q.Url, q.IsTopLevel);
                seededCount++;
            }

            foreach (var pr in seedData.PullRequestSearches ?? [])
            {
                PullRequestSearch.AddOrUpdate(persistentDataStore, pr.Url, pr.Name, pr.View, pr.IsTopLevel);
                seededCount++;
            }

            foreach (var p in seedData.PipelineSearches ?? [])
            {
                DefinitionSearch.AddOrUpdate(persistentDataStore, p.Name, p.InternalId, p.Url, p.IsTopLevel);
                seededCount++;
            }

            foreach (var bl in seedData.BoardLinks ?? [])
            {
                BoardLink.AddOrUpdate(persistentDataStore, bl.Url, bl.DisplayName);
                seededCount++;
            }

            _log.Information("Seeded {Count} entries from seed-data.json.", seededCount);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to seed persistent data store from seed-data.json.");
        }
    }

    private static bool IsDatabaseEmpty(DataStore dataStore)
    {
        var tables = new[] { "ProjectSettings", "Query", "PullRequestSearch", "DefinitionSearch", "BoardLink" };
        foreach (var table in tables)
        {
            var count = dataStore.Connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM [{table}]");
            if (count > 0)
            {
                return false;
            }
        }

        return true;
    }

    private sealed class SeedData
    {
        public List<SeedProjectSettings>? ProjectSettings { get; set; }

        public List<SeedQuery>? Queries { get; set; }

        public List<SeedPullRequestSearch>? PullRequestSearches { get; set; }

        public List<SeedPipelineSearch>? PipelineSearches { get; set; }

        public List<SeedBoardLink>? BoardLinks { get; set; }
    }

    private sealed class SeedProjectSettings
    {
        public string OrganizationUrl { get; set; } = string.Empty;

        public string ProjectName { get; set; } = string.Empty;
    }

    private sealed class SeedQuery
    {
        public string Name { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public bool IsTopLevel { get; set; }
    }

    private sealed class SeedPullRequestSearch
    {
        public string Url { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string View { get; set; } = string.Empty;

        public bool IsTopLevel { get; set; }
    }

    private sealed class SeedPipelineSearch
    {
        public string Name { get; set; } = string.Empty;

        public long InternalId { get; set; }

        public string Url { get; set; } = string.Empty;

        public bool IsTopLevel { get; set; }
    }

    private sealed class SeedBoardLink
    {
        public string Url { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;
    }
}
