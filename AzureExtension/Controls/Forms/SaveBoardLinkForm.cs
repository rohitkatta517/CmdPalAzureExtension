// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using AzureExtension.Client;
using AzureExtension.Helpers;
using AzureExtension.PersistentData;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Serilog;

namespace AzureExtension.Controls.Forms;

public sealed class SaveBoardLinkForm : FormContent
{
    private readonly ILogger _log = Log.Logger.ForContext("SourceContext", nameof(SaveBoardLinkForm));
    private readonly BoardLinkRepository _repository;
    private readonly SavedAzureSearchesMediator _mediator;
    private readonly BoardLink? _existingLink;

    public bool IsEditing => _existingLink != null;

    private string ExistingUrlValue => _existingLink?.Url ?? string.Empty;

    private Dictionary<string, string> TemplateSubstitutions => new()
    {
        { "{{SaveBoardLinkFormTitle}}", IsEditing ? "Edit Board Link" : "Add a Board Link" },
        { "{{AzureDevOpsBoardUrlValue}}", ExistingUrlValue },
        { "{{SaveBoardLinkActionTitle}}", "Save Board Link" },
    };

    public override string TemplateJson => TemplateHelper.LoadTemplateJsonFromTemplateName("SaveBoardLink", TemplateSubstitutions);

    public SaveBoardLinkForm(
        BoardLink? existingLink,
        BoardLinkRepository repository,
        SavedAzureSearchesMediator mediator)
    {
        _existingLink = existingLink;
        _repository = repository;
        _mediator = mediator;
    }

    public override ICommandResult SubmitForm(string inputs, string data)
    {
        try
        {
            var payloadJson = JsonNode.Parse(inputs);
            var url = payloadJson?["AzureDevOpsBoardUrl"]?.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(url))
            {
                ToastHelper.ShowErrorToast("Board URL is required.");
                return CommandResult.KeepOpen();
            }

            // If editing and URL changed, remove the old entry
            if (IsEditing && !string.Equals(_existingLink!.Url, url, StringComparison.OrdinalIgnoreCase))
            {
                _repository.Remove(_existingLink.Id);
            }

            var displayName = ExtractDisplayName(url);

            _repository.AddOrUpdate(url, displayName);
            _mediator.AddSearch(null, SearchUpdatedType.BoardLink);

            ToastHelper.ShowSuccessToast($"Board link '{displayName}' saved successfully.");
            return CommandResult.GoHome();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error saving board link: {Message}", ex.Message);
            ToastHelper.ShowErrorToast($"Failed to save board link: {ex.Message}");
            return CommandResult.KeepOpen();
        }
    }

    internal static string ExtractDisplayName(string url)
    {
        // Try to parse board URL to extract team name and backlog level.
        // Supported patterns:
        //   .../_{boards|backlogs}/board/t/{TeamName}/{BacklogLevel}
        try
        {
            var uri = new Uri(url);
            var segments = uri.Segments
                .Select(s => Uri.UnescapeDataString(s.TrimEnd('/')))
                .ToArray();

            // Find the index of the segment starting with "_boards" or "_backlogs"
            var boardSegmentIndex = -1;
            for (var i = 0; i < segments.Length; i++)
            {
                if (segments[i].Equals("_boards", StringComparison.OrdinalIgnoreCase) ||
                    segments[i].Equals("_backlogs", StringComparison.OrdinalIgnoreCase))
                {
                    boardSegmentIndex = i;
                    break;
                }
            }

            if (boardSegmentIndex >= 0)
            {
                // Look for /board/t/{team}/{level} or /backlog/t/{team}/{level}
                // Pattern: _boards/board/t/{team}/{level}
                var tIndex = -1;
                for (var i = boardSegmentIndex + 1; i < segments.Length; i++)
                {
                    if (segments[i].Equals("t", StringComparison.OrdinalIgnoreCase))
                    {
                        tIndex = i;
                        break;
                    }
                }

                if (tIndex >= 0 && tIndex + 1 < segments.Length)
                {
                    var teamName = segments[tIndex + 1];

                    if (tIndex + 2 < segments.Length)
                    {
                        var backlogLevel = segments[tIndex + 2];
                        return $"{teamName} / {backlogLevel}";
                    }

                    return teamName;
                }
            }
        }
        catch
        {
            // Fall through to use URL as display name
        }

        return url;
    }

    internal static string ExtractSubtitle(string url)
    {
        var azureUri = new AzureUri(url);
        if (azureUri.IsValid && !string.IsNullOrEmpty(azureUri.Organization))
        {
            var project = azureUri.Project;
            return string.IsNullOrEmpty(project)
                ? azureUri.Organization
                : $"{azureUri.Organization} / {project}";
        }

        return string.Empty;
    }
}
