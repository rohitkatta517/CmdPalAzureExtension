// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.DataModel;

namespace AzureExtension.Controls;

public interface IPullRequest
{
    string Title { get; set; }

    string Url { get; set; }

    long InternalId { get; set; }

    long RepositoryId { get; set; }

    string Status { get; set; }

    string PolicyStatus { get; set; }

    string PolicyStatusReason { get; set; }

    Identity? Creator { get; }

    string TargetBranch { get; set; }

    long CreationDate { get; set; }

    string HtmlUrl { get; set; }

    string RepositoryGuid { get; }

    long ApprovedCount { get; set; }

    long RejectCount { get; set; }

    long WaitForAuthorCount { get; set; }

    long ReviewerCount { get; set; }

    long ActiveCommentCount { get; set; }

    long IsDraft { get; set; }
}
