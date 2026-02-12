// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Controls.Commands;
using AzureExtension.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace AzureExtension.Controls.Pages;

public sealed partial class PullRequestSearchPage : SearchPage<IPullRequest>
{
    private readonly IResources _resources;
    private readonly TimeSpanHelper _timeSpanHelper;

    public PullRequestSearchPage(
        IPullRequestSearch search,
        IResources resources,
        ILiveContentDataProvider<IPullRequest> contentDataProvider,
        TimeSpanHelper timeSpanHelper)
        : base(search, contentDataProvider, resources)
    {
        _resources = resources;
        _timeSpanHelper = timeSpanHelper;
        Icon = IconLoader.GetIcon("PullRequest");
        Title = search.Name;
        Name = Title; // Name is for the command, title is for the page
        ShowDetails = true;
    }

    protected override ListItem GetListItem(IPullRequest item)
    {
        var title = item.Title;
        var url = item.HtmlUrl;

        return new ListItem(new LinkCommand(url, _resources, null))
        {
            Title = title,
            Icon = IconLoader.GetIconForPullRequestStatus(item.PolicyStatus),
            Tags = BuildTags(item),
            MoreCommands = new CommandContextItem[]
            {
                new(new CopyCommand(item.InternalId.ToStringInvariant(), _resources.GetResource("Pages_PullRequestSearchPage_CopyIdCommand"), _resources)),
                new(new CopyCommand(item.HtmlUrl, _resources.GetResource("Pages_PullRequestSearchPage_CopyURLCommand"), _resources)),
            },
            Details = new Details()
            {
                Title = item.Title,
                Metadata = BuildDetailsMetadata(item),
            },
        };
    }

    private ITag[] BuildTags(IPullRequest item)
    {
        var tags = new List<ITag>();

        if (item.IsDraft == 1)
        {
            tags.Add(new Tag()
            {
                Text = _resources.GetResource("Pages_PullRequestSearchPage_Draft"),
                Icon = IconLoader.GetIcon("StatusGray"),
            });
        }

        if (item.ReviewerCount > 0)
        {
            tags.Add(GetApprovalTag(item));
        }

        if (item.ActiveCommentCount > 0)
        {
            tags.Add(new Tag()
            {
                Text = $"{item.ActiveCommentCount} active",
                Icon = IconLoader.GetIcon("StatusOrange"),
            });
        }

        return tags.ToArray();
    }

    private static Tag GetApprovalTag(IPullRequest item)
    {
        string iconKey;
        if (item.RejectCount > 0)
        {
            iconKey = "StatusRed";
        }
        else if (item.ApprovedCount == item.ReviewerCount)
        {
            iconKey = "StatusGreen";
        }
        else if (item.ApprovedCount > 0)
        {
            iconKey = "StatusBlue";
        }
        else
        {
            iconKey = "StatusGray";
        }

        return new Tag()
        {
            Text = $"{item.ApprovedCount}/{item.ReviewerCount} approved",
            Icon = IconLoader.GetIcon(iconKey),
        };
    }

    private DetailsElement[] BuildDetailsMetadata(IPullRequest item)
    {
        var metadata = new List<DetailsElement>
        {
            new()
            {
                Key = _resources.GetResource("Pages_PullRequestSearchPage_Author"),
                Data = new DetailsLink() { Text = $"{item.Creator?.Name}" },
            },
            new()
            {
                Key = _resources.GetResource("Pages_PullRequestSearchPage_UpdatedAt"),
                Data = new DetailsLink() { Text = $"{_timeSpanHelper.DateTimeOffsetToDisplayString(item.Creator?.UpdatedAt, null)}" },
            },
            new()
            {
                Key = _resources.GetResource("Pages_PullRequestSearchPage_TargetBranch"),
                Data = new DetailsLink() { Text = $"{item.TargetBranch}" },
            },
            new()
            {
                Key = _resources.GetResource("Pages_PullRequestSearchPage_PolicyStatus"),
                Data = new DetailsLink() { Text = $"{item.PolicyStatus}" },
            },
            new()
            {
                Key = _resources.GetResource("Pages_PullRequestSearchPage_PolicyStatusReason"),
                Data = new DetailsLink() { Text = !string.IsNullOrEmpty(item.PolicyStatusReason) ? $"{item.PolicyStatusReason}" : _resources.GetResource("Pages_PullRequestSearchPage_PolicyStatusReasonNone") },
            },
        };

        if (item.ReviewerCount > 0)
        {
            metadata.Add(new DetailsElement()
            {
                Key = _resources.GetResource("Pages_PullRequestSearchPage_Reviewers"),
                Data = new DetailsLink() { Text = BuildReviewerSummary(item) },
            });
        }

        if (item.ActiveCommentCount >= 0)
        {
            metadata.Add(new DetailsElement()
            {
                Key = _resources.GetResource("Pages_PullRequestSearchPage_Comments"),
                Data = new DetailsLink() { Text = $"{item.ActiveCommentCount} active" },
            });
        }

        metadata.Add(new DetailsElement()
        {
            Key = _resources.GetResource("Pages_PullRequestSearchPage_InternalId"),
            Data = new DetailsLink() { Text = $"{item.InternalId}" },
        });
        metadata.Add(new DetailsElement()
        {
            Key = _resources.GetResource("Pages_PullRequestSearchPage_CreationDate"),
            Data = new DetailsLink() { Text = $"{new DateTime(item.CreationDate)}" },
        });

        if (item.IsDraft == 1 || item.ReviewerCount > 0 || item.ActiveCommentCount > 0)
        {
            metadata.Add(new DetailsElement()
            {
                Data = new DetailsTags()
                {
                    Tags = BuildTags(item),
                },
            });
        }

        return metadata.ToArray();
    }

    private static string BuildReviewerSummary(IPullRequest item)
    {
        var parts = new List<string>();
        if (item.ApprovedCount > 0)
        {
            parts.Add($"{item.ApprovedCount} approved");
        }

        if (item.WaitForAuthorCount > 0)
        {
            parts.Add($"{item.WaitForAuthorCount} wait for author");
        }

        if (item.RejectCount > 0)
        {
            parts.Add($"{item.RejectCount} rejected");
        }

        var noVoteCount = item.ReviewerCount - item.ApprovedCount - item.WaitForAuthorCount - item.RejectCount;
        if (noVoteCount > 0)
        {
            parts.Add($"{noVoteCount} no vote");
        }

        return parts.Count > 0 ? string.Join(", ", parts) : $"{item.ReviewerCount} reviewers";
    }
}
