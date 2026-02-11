// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Controls;
using AzureExtension.Controls.Forms;
using AzureExtension.Controls.Pages;
using AzureExtension.Helpers;
using AzureExtension.PersistentData;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AzureExtension;

public partial class SavedProjectsPage : SavedSearchesPage
{
    private readonly IListItem _addProjectListItem;
    private readonly ProjectSettingsRepository _projectSettingsRepository;
    private readonly SavedAzureSearchesMediator _savedSearchesMediator;

    protected override SearchUpdatedType SearchUpdatedType => SearchUpdatedType.ProjectSettings;

    protected override string ExceptionMessage => "Failed to update saved projects.";

    public SavedProjectsPage(
       IListItem addProjectListItem,
       SavedAzureSearchesMediator mediator,
       ProjectSettingsRepository projectSettingsRepository)
        : base(mediator)
    {
        Title = "Saved Azure DevOps Projects";
        Name = Title;
        Icon = IconLoader.GetIcon("QueryList");
        _addProjectListItem = addProjectListItem;
        _projectSettingsRepository = projectSettingsRepository;
        _savedSearchesMediator = mediator;
    }

    public override IListItem[] GetItems()
    {
        var searches = _projectSettingsRepository.GetSavedSearches(false);

        if (searches.Any())
        {
            var items = searches.Select(s =>
            {
                var editForm = new SaveProjectSettingsForm(s, _projectSettingsRepository, _savedSearchesMediator);
                var editPage = new SaveProjectSettingsPage(editForm);
                var item = new ListItem(editPage)
                {
                    Title = s.ProjectName,
                    Subtitle = s.OrganizationUrl,
                };
                return (IListItem)item;
            }).ToList();

            items.Add(_addProjectListItem);

            return items.ToArray();
        }
        else
        {
            return [_addProjectListItem];
        }
    }
}
