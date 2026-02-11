// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Controls.Forms;
using AzureExtension.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AzureExtension.Controls.Pages;

public sealed class SaveProjectSettingsPage : ContentPage
{
    private readonly SaveProjectSettingsForm _form;

    public SaveProjectSettingsPage(SaveProjectSettingsForm form)
    {
        _form = form;
        Icon = _form.IsEditing ? IconLoader.GetIcon("Edit") : IconLoader.GetIcon("Add");
        Title = _form.IsEditing ? "Edit Project" : "Add a Project";
        Name = Title;
    }

    public override IContent[] GetContent()
    {
        return [_form];
    }
}
