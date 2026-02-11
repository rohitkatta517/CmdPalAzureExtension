// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Controls.Pages;
using AzureExtension.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AzureExtension.Controls.ListItems;

public partial class AddProjectListItem : ListItem
{
    public AddProjectListItem(SaveProjectSettingsPage page)
    : base(page)
    {
        Title = "Add a project";
        Icon = IconLoader.GetIcon("Add");
    }
}
