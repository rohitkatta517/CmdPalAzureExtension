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

public partial class SavedBoardLinksPage : SavedSearchesPage
{
    private readonly IListItem _addBoardLinkListItem;
    private readonly BoardLinkRepository _boardLinkRepository;
    private readonly SavedAzureSearchesMediator _savedSearchesMediator;

    protected override SearchUpdatedType SearchUpdatedType => SearchUpdatedType.BoardLink;

    protected override string ExceptionMessage => "Failed to update saved board links.";

    public SavedBoardLinksPage(
       IListItem addBoardLinkListItem,
       SavedAzureSearchesMediator mediator,
       BoardLinkRepository boardLinkRepository)
        : base(mediator)
    {
        Title = "Saved Board Links";
        Name = Title;
        Icon = IconLoader.GetIcon("Board");
        _addBoardLinkListItem = addBoardLinkListItem;
        _boardLinkRepository = boardLinkRepository;
        _savedSearchesMediator = mediator;
    }

    public override IListItem[] GetItems()
    {
        var boardLinks = _boardLinkRepository.GetAll();

        if (boardLinks.Any())
        {
            var items = boardLinks.Select(link =>
            {
                var editForm = new SaveBoardLinkForm(link, _boardLinkRepository, _savedSearchesMediator);
                var editPage = new SaveBoardLinkPage(editForm);
                var item = new ListItem(editPage)
                {
                    Title = link.DisplayName,
                    Subtitle = SaveBoardLinkForm.ExtractSubtitle(link.Url),
                };
                return (IListItem)item;
            }).ToList();

            items.Add(_addBoardLinkListItem);

            return items.ToArray();
        }
        else
        {
            return [_addBoardLinkListItem];
        }
    }
}
