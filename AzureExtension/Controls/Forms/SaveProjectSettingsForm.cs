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

public sealed class SaveProjectSettingsForm : FormContent
{
    private readonly ILogger _log = Log.Logger.ForContext("SourceContext", nameof(SaveProjectSettingsForm));
    private readonly ProjectSettingsRepository _repository;
    private readonly SavedAzureSearchesMediator _mediator;
    private readonly IMyWorkItemsSearch? _existingSearch;

    public bool IsEditing => _existingSearch != null;

    private string ExistingUrlValue
    {
        get
        {
            if (_existingSearch == null)
            {
                return string.Empty;
            }

            // Reconstruct a displayable URL from org + project
            return $"{_existingSearch.OrganizationUrl.TrimEnd('/')}/{_existingSearch.ProjectName}";
        }
    }

    private Dictionary<string, string> TemplateSubstitutions => new()
    {
        { "{{SaveProjectSettingsFormTitle}}", IsEditing ? "Edit Project" : "Add a Project" },
        { "{{AzureDevOpsUrlLabel}}", "Azure DevOps URL" },
        { "{{AzureDevOpsUrlErrorMessage}}", "A valid Azure DevOps URL is required" },
        { "{{AzureDevOpsUrlValue}}", ExistingUrlValue },
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
            var url = payloadJson?["AzureDevOpsUrl"]?.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(url))
            {
                ToastHelper.ShowErrorToast("Azure DevOps URL is required.");
                return CommandResult.KeepOpen();
            }

            var azureUri = new AzureUri(url);
            if (!azureUri.IsValid || string.IsNullOrEmpty(azureUri.Organization))
            {
                ToastHelper.ShowErrorToast("Could not parse the URL. Use a URL like https://dev.azure.com/myorg/myproject");
                return CommandResult.KeepOpen();
            }

            var organizationUrl = azureUri.Connection.ToString().TrimEnd('/');
            var projectName = azureUri.Project;

            if (string.IsNullOrEmpty(projectName))
            {
                ToastHelper.ShowErrorToast("Could not find a project in the URL. Include the project, e.g. https://dev.azure.com/myorg/myproject");
                return CommandResult.KeepOpen();
            }

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
