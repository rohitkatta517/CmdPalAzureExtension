// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using AzureExtension.Helpers;
using AzureExtension.PersistentData;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Serilog;

namespace AzureExtension.Controls.Forms;

public sealed class SaveProjectSettingsForm : FormContent
{
    private readonly ILogger _log = Log.Logger.ForContext("SourceContext", nameof(SaveProjectSettingsForm));
    private readonly ProjectSettingsRepository _repository;
    private readonly SavedAzureSearchesMediator _mediator;
    private readonly IMyWorkItemsSearch? _existingSearch;

    public bool IsEditing => _existingSearch != null;

    private Dictionary<string, string> TemplateSubstitutions => new()
    {
        { "{{SaveProjectSettingsFormTitle}}", IsEditing ? "Edit Project" : "Add a Project" },
        { "{{OrganizationUrlLabel}}", "Organization URL" },
        { "{{OrganizationUrlErrorMessage}}", "Organization URL is required" },
        { "{{OrganizationUrlValue}}", _existingSearch?.OrganizationUrl ?? string.Empty },
        { "{{ProjectNameLabel}}", "Project Name" },
        { "{{ProjectNameErrorMessage}}", "Project name is required" },
        { "{{ProjectNameValue}}", _existingSearch?.ProjectName ?? string.Empty },
        { "{{SaveProjectSettingsActionTitle}}", "Save Project" },
    };

    public override string TemplateJson => TemplateHelper.LoadTemplateJsonFromTemplateName("SaveProjectSettings", TemplateSubstitutions);

    public SaveProjectSettingsForm(
        IMyWorkItemsSearch? existingSearch,
        ProjectSettingsRepository repository,
        SavedAzureSearchesMediator mediator)
    {
        _existingSearch = existingSearch;
        _repository = repository;
        _mediator = mediator;
    }

    public override ICommandResult SubmitForm(string inputs, string data)
    {
        try
        {
            var payloadJson = JsonNode.Parse(inputs);
            var organizationUrl = payloadJson?["OrganizationUrl"]?.ToString()?.Trim() ?? string.Empty;
            var projectName = payloadJson?["ProjectName"]?.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(organizationUrl) || string.IsNullOrEmpty(projectName))
            {
                ToastHelper.ShowErrorToast("Organization URL and project name are required.");
                return CommandResult.KeepOpen();
            }

            // Normalize: ensure URL doesn't end with /
            organizationUrl = organizationUrl.TrimEnd('/');

            // If editing and the org/project key changed, remove the old entry first
            if (IsEditing &&
                (_existingSearch!.OrganizationUrl.TrimEnd('/') != organizationUrl ||
                 !string.Equals(_existingSearch.ProjectName, projectName, StringComparison.OrdinalIgnoreCase)))
            {
                _repository.RemoveSavedSearch(_existingSearch);
            }

            var search = new MyWorkItemsSearch(
                $"My Work Items - {projectName}",
                organizationUrl,
                projectName);

            _repository.AddOrUpdateSearch(search);
            _mediator.AddSearch(search, SearchUpdatedType.ProjectSettings);

            ToastHelper.ShowSuccessToast($"Project '{projectName}' saved successfully.");
            return CommandResult.GoHome();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Error saving project settings: {Message}", ex.Message);
            ToastHelper.ShowErrorToast($"Failed to save project: {ex.Message}");
            return CommandResult.KeepOpen();
        }
    }
}
