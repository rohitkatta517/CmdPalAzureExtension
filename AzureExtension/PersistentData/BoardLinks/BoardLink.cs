// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Data;
using Dapper;
using Dapper.Contrib.Extensions;
using Serilog;

namespace AzureExtension.PersistentData;

[Table("BoardLink")]
public class BoardLink
{
    private static readonly Lazy<ILogger> _logger = new(() => Log.ForContext("SourceContext", $"PersistentData/{nameof(BoardLink)}"));

    private static readonly ILogger _log = _logger.Value;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public string Url { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public static BoardLink? Get(DataStore datastore, long id)
    {
        var sql = "SELECT * FROM BoardLink WHERE Id = @Id";
        return datastore.Connection.QueryFirstOrDefault<BoardLink>(sql, new { Id = id });
    }

    public static BoardLink? GetByUrl(DataStore datastore, string url)
    {
        var sql = "SELECT * FROM BoardLink WHERE Url = @Url";
        return datastore.Connection.QueryFirstOrDefault<BoardLink>(sql, new { Url = url });
    }

    public static BoardLink Add(DataStore datastore, string url, string displayName)
    {
        var boardLink = new BoardLink
        {
            Url = url,
            DisplayName = displayName,
        };

        datastore.Connection.Insert(boardLink);
        return boardLink;
    }

    public static void Remove(DataStore datastore, long id)
    {
        var sql = "DELETE FROM BoardLink WHERE Id = @Id";
        var command = datastore.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Id", id);
        _log.Verbose(DataStore.GetCommandLogMessage(sql, command));
        var deleted = command.ExecuteNonQuery();
        _log.Verbose($"Deleted {deleted} rows from BoardLink table.");
    }

    public static IEnumerable<BoardLink> GetAll(DataStore datastore)
    {
        var sql = "SELECT * FROM BoardLink";
        return datastore.Connection.Query<BoardLink>(sql);
    }

    public static void AddOrUpdate(DataStore datastore, string url, string displayName)
    {
        var existing = GetByUrl(datastore, url);
        if (existing != null)
        {
            var sql = "UPDATE BoardLink SET DisplayName = @DisplayName WHERE Id = @Id";
            datastore.Connection.Execute(sql, new { DisplayName = displayName, Id = existing.Id });
            return;
        }

        Add(datastore, url, displayName);
    }
}
