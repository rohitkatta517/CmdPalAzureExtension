// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace AzureExtension.Controls;

public class MyWorkItemsSearch : IMyWorkItemsSearch
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public bool IsTopLevel { get; set; }

    public string OrganizationUrl { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public MyWorkItemsSearch(string name, string organizationUrl, string projectName)
    {
        Name = name;
        OrganizationUrl = organizationUrl;
        ProjectName = projectName;
        Url = organizationUrl;
        IsTopLevel = false;
    }
}
