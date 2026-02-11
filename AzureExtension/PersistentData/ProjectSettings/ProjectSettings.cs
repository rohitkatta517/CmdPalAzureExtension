// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Controls;
using AzureExtension.Data;
using Dapper;
using Dapper.Contrib.Extensions;
using Serilog;

namespace AzureExtension.PersistentData;

[Table("ProjectSettings")]
public class ProjectSettings : IMyWorkItemsSearch
{
    private static readonly Lazy<ILogger> _logger = new(() => Log.ForContext("SourceContext", $"PersistentData/{nameof(ProjectSettings)}"));

    private static readonly ILogger _log = _logger.Value;

    [Key]
    public long Id { get; set; } = DataStore.NoForeignKey;

    public string OrganizationUrl { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    [Computed]
    [Write(false)]
    public string Name => $"My Work Items - {ProjectName}";

    [Computed]
    [Write(false)]
    public string Url => OrganizationUrl;

    [Computed]
    [Write(false)]
    public bool IsTopLevel => false;

    public static ProjectSettings? Get(DataStore datastore, string organizationUrl, string projectName)
    {
        var sql = "SELECT * FROM ProjectSettings WHERE OrganizationUrl = @OrganizationUrl AND ProjectName = @ProjectName";
        var result = datastore.Connection.QueryFirstOrDefault<ProjectSettings>(sql, new { OrganizationUrl = organizationUrl, ProjectName = projectName });
        return result;
    }

    public static ProjectSettings Add(DataStore datastore, string organizationUrl, string projectName)
    {
        var project = new ProjectSettings
        {
            OrganizationUrl = organizationUrl,
            ProjectName = projectName,
        };

        datastore.Connection.Insert(project);
        return project;
    }

    public static void Remove(DataStore datastore, string organizationUrl, string projectName)
    {
        var sql = "DELETE FROM ProjectSettings WHERE OrganizationUrl = @OrganizationUrl AND ProjectName = @ProjectName";
        var command = datastore.Connection!.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@OrganizationUrl", organizationUrl);
        command.Parameters.AddWithValue("@ProjectName", projectName);
        _log.Verbose(DataStore.GetCommandLogMessage(sql, command));
        var deleted = command.ExecuteNonQuery();
        _log.Verbose($"Deleted {deleted} rows from ProjectSettings table.");
    }

    public static IEnumerable<IMyWorkItemsSearch> GetAll(DataStore datastore)
    {
        var sql = "SELECT * FROM ProjectSettings";
        var results = datastore.Connection.Query<ProjectSettings>(sql);
        return results;
    }

    public static void AddOrUpdate(DataStore datastore, string organizationUrl, string projectName)
    {
        var existing = Get(datastore, organizationUrl, projectName);
        if (existing != null)
        {
            return;
        }

        Add(datastore, organizationUrl, projectName);
    }
}
