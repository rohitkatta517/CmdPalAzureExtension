// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Account;
using AzureExtension.Client;
using AzureExtension.Controls;
using AzureExtension.DataManager;
using AzureExtension.DataModel;
using Microsoft.Identity.Client;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Profile;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Query = AzureExtension.DataModel.Query;
using TFModels = Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using WorkItem = AzureExtension.DataModel.WorkItem;

namespace AzureExtension.Test.DataManager;

[TestClass]
public class MyWorkItemsManagerTests
{
    [TestMethod]
    public void TestMyWorkItemsSearchProperties()
    {
        var search = new MyWorkItemsSearch("My Work Items - TestProject", "https://dev.azure.com/testorg/", "TestProject");

        Assert.AreEqual("My Work Items - TestProject", search.Name);
        Assert.AreEqual("https://dev.azure.com/testorg/", search.OrganizationUrl);
        Assert.AreEqual("TestProject", search.ProjectName);
        Assert.AreEqual("https://dev.azure.com/testorg/", search.Url);
        Assert.IsFalse(search.IsTopLevel);
    }

    [TestMethod]
    public void TestGetQueryIdForSearch()
    {
        var search = new MyWorkItemsSearch("My Work Items", "https://dev.azure.com/testorg/", "TestProject");
        var queryId = AzureDataMyWorkItemsManager.GetQueryIdForSearch(search);

        Assert.IsTrue(queryId.StartsWith(AzureDataMyWorkItemsManager.MyWorkItemsQueryIdPrefix, StringComparison.Ordinal));
        Assert.IsTrue(queryId.Contains("testorg"));
        Assert.IsTrue(queryId.Contains("TestProject"));
    }

    [TestMethod]
    public void TestDiscoverSearchesFromSavedSearches()
    {
        var dataStore = DataManagerTests.GetTestDataStore();
        var mockAccountProvider = new Mock<IAccountProvider>();
        var mockLiveDataProvider = new Mock<IAzureLiveDataProvider>();
        var mockConnectionProvider = new Mock<IConnectionProvider>();

        var mockQuerySearch = new Mock<IAzureSearch>();
        mockQuerySearch.SetupGet(s => s.Url).Returns("https://dev.azure.com/testorg/TestProject/_queries/query/12345678-1234-1234-1234-1234567890ab");
        mockQuerySearch.SetupGet(s => s.Name).Returns("Test Query");

        var mockPRSearch = new Mock<IAzureSearch>();
        mockPRSearch.SetupGet(s => s.Url).Returns("https://dev.azure.com/testorg/TestProject/_git/TestRepo");
        mockPRSearch.SetupGet(s => s.Name).Returns("Test PR Search");

        var mockDiffOrgSearch = new Mock<IAzureSearch>();
        mockDiffOrgSearch.SetupGet(s => s.Url).Returns("https://dev.azure.com/otherorg/OtherProject/_queries/query/87654321-4321-4321-4321-ba0987654321");
        mockDiffOrgSearch.SetupGet(s => s.Name).Returns("Other Query");

        var mockRepo1 = new Mock<IAzureSearchRepository>();
        mockRepo1.Setup(r => r.GetAll(false)).Returns(new List<IAzureSearch> { mockQuerySearch.Object, mockDiffOrgSearch.Object });

        var mockRepo2 = new Mock<IAzureSearchRepository>();
        mockRepo2.Setup(r => r.GetAll(false)).Returns(new List<IAzureSearch> { mockPRSearch.Object });

        var searchRepositories = new List<IAzureSearchRepository> { mockRepo1.Object, mockRepo2.Object };

        var manager = new AzureDataMyWorkItemsManager(dataStore, mockAccountProvider.Object, mockLiveDataProvider.Object, mockConnectionProvider.Object, searchRepositories);

        var discovered = manager.DiscoverSearches().ToList();

        // Should discover 2 unique org/project pairs (testorg/TestProject is deduped)
        Assert.AreEqual(2, discovered.Count);
        Assert.IsTrue(discovered.Any(s => s.ProjectName == "TestProject"));
        Assert.IsTrue(discovered.Any(s => s.ProjectName == "OtherProject"));

        DataManagerTests.CleanUpDataStore(dataStore);
    }

    [TestMethod]
    public void TestDiscoverSearchesDeduplicates()
    {
        var dataStore = DataManagerTests.GetTestDataStore();
        var mockAccountProvider = new Mock<IAccountProvider>();
        var mockLiveDataProvider = new Mock<IAzureLiveDataProvider>();
        var mockConnectionProvider = new Mock<IConnectionProvider>();

        // Two searches in the same org/project
        var mockSearch1 = new Mock<IAzureSearch>();
        mockSearch1.SetupGet(s => s.Url).Returns("https://dev.azure.com/testorg/TestProject/_queries/query/11111111-1111-1111-1111-111111111111");
        mockSearch1.SetupGet(s => s.Name).Returns("Query 1");

        var mockSearch2 = new Mock<IAzureSearch>();
        mockSearch2.SetupGet(s => s.Url).Returns("https://dev.azure.com/testorg/TestProject/_queries/query/22222222-2222-2222-2222-222222222222");
        mockSearch2.SetupGet(s => s.Name).Returns("Query 2");

        var mockRepo = new Mock<IAzureSearchRepository>();
        mockRepo.Setup(r => r.GetAll(false)).Returns(new List<IAzureSearch> { mockSearch1.Object, mockSearch2.Object });

        var searchRepositories = new List<IAzureSearchRepository> { mockRepo.Object };

        var manager = new AzureDataMyWorkItemsManager(dataStore, mockAccountProvider.Object, mockLiveDataProvider.Object, mockConnectionProvider.Object, searchRepositories);

        var discovered = manager.DiscoverSearches().ToList();

        // Two searches in same org/project should result in just one MyWorkItemsSearch
        Assert.AreEqual(1, discovered.Count);
        Assert.AreEqual("TestProject", discovered[0].ProjectName);

        DataManagerTests.CleanUpDataStore(dataStore);
    }

    [TestMethod]
    public async Task TestMyWorkItemsUpdateFlow()
    {
        var dataStore = DataManagerTests.GetTestDataStore();
        var mockAccountProvider = new Mock<IAccountProvider>();
        var mockLiveDataProvider = new Mock<IAzureLiveDataProvider>();
        var mockConnectionProvider = new Mock<IConnectionProvider>();
        var searchRepositories = new List<IAzureSearchRepository>();

        var manager = new AzureDataMyWorkItemsManager(dataStore, mockAccountProvider.Object, mockLiveDataProvider.Object, mockConnectionProvider.Object, searchRepositories);

        var mockVssConnection = new Mock<IVssConnection>();
        var stubIdentity = new Microsoft.VisualStudio.Services.Identity.Identity()
        {
            Id = Guid.NewGuid(),
        };

        mockVssConnection.Setup(c => c.AuthorizedIdentity).Returns(stubIdentity);

        mockConnectionProvider
            .Setup(c => c.GetVssConnectionAsync(It.IsAny<Uri>(), It.IsAny<IAccount>()))
            .ReturnsAsync(mockVssConnection.Object);

        var stubAccount = new Mock<IAccount>();
        stubAccount.SetupGet(a => a.Username).Returns("TestUsername");

        mockAccountProvider.Setup(a => a.GetDefaultAccountAsync()).ReturnsAsync(stubAccount.Object);
        mockAccountProvider.Setup(a => a.GetDefaultAccount()).Returns(stubAccount.Object);

        mockLiveDataProvider.Setup(p => p.GetTeamProject(It.IsAny<IVssConnection>(), It.IsAny<string>()))
            .ReturnsAsync(new TeamProject
            {
                Id = Guid.NewGuid(),
                Name = "TestProject",
                Url = "https://dev.azure.com/testorg/TestProject",
            });

        mockLiveDataProvider.Setup(p => p.QueryByWiqlAsync(It.IsAny<IVssConnection>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkItemQueryResult
            {
                QueryType = QueryType.Flat,
                WorkItems =
                [
                    new WorkItemReference
                    {
                        Id = 42,
                        Url = "https://dev.azure.com/testorg/TestProject/_apis/wit/workitems/42",
                    },
                ],
            });

        mockLiveDataProvider.Setup(p => p.GetWorkItemsAsync(It.IsAny<IVssConnection>(), It.IsAny<string>(), It.IsAny<IEnumerable<int>>(), It.IsAny<WorkItemExpand>(), It.IsAny<WorkItemErrorPolicy>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new TFModels.WorkItem
                {
                    Id = 42,
                    Fields =
                    {
                        ["System.Title"] = "My Active Task",
                        ["System.State"] = "Active",
                        ["System.WorkItemType"] = "Task",
                    },
                },
            ]);

        mockLiveDataProvider.Setup(p => p.GetWorkItemTypeAsync(It.IsAny<IVssConnection>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TFModels.WorkItemType
            {
                Name = "Task",
                Url = "https://dev.azure.com/testorg/TestProject/_apis/wit/workitemtypes/Task",
            });

        mockLiveDataProvider.Setup(p => p.GetAvatarAsync(It.IsAny<IVssConnection>(), It.IsAny<Guid>()))
            .ReturnsAsync(new Avatar
            {
                Value = Array.Empty<byte>(),
            });

        var testSearch = new MyWorkItemsSearch("My Work Items - TestProject", "https://dev.azure.com/testorg/", "TestProject");

        await manager.UpdateMyWorkItemsAsync(testSearch, CancellationToken.None);

        // Verify WIQL was used (not query by ID)
        mockLiveDataProvider.Verify(
            p => p.QueryByWiqlAsync(
                It.IsAny<IVssConnection>(),
                It.IsAny<string>(),
                It.Is<string>(wiql => wiql.Contains("@Me") && wiql.Contains("Closed")),
                It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify work items were stored in the data store
        var queryId = AzureDataMyWorkItemsManager.GetQueryIdForSearch(testSearch);
        var dsQuery = Query.Get(dataStore, queryId, "TestUsername");
        Assert.IsNotNull(dsQuery);

        var workItems = WorkItem.GetForQuery(dataStore, dsQuery).ToList();
        Assert.AreEqual(1, workItems.Count);
        Assert.AreEqual("My Active Task", workItems[0].SystemTitle);

        DataManagerTests.CleanUpDataStore(dataStore);
    }

    [TestMethod]
    public void TestDiscoverSearchesWithInvalidUrls()
    {
        var dataStore = DataManagerTests.GetTestDataStore();
        var mockAccountProvider = new Mock<IAccountProvider>();
        var mockLiveDataProvider = new Mock<IAzureLiveDataProvider>();
        var mockConnectionProvider = new Mock<IConnectionProvider>();

        var mockInvalidSearch = new Mock<IAzureSearch>();
        mockInvalidSearch.SetupGet(s => s.Url).Returns("not-a-valid-url");
        mockInvalidSearch.SetupGet(s => s.Name).Returns("Invalid");

        var mockRepo = new Mock<IAzureSearchRepository>();
        mockRepo.Setup(r => r.GetAll(false)).Returns(new List<IAzureSearch> { mockInvalidSearch.Object });

        var searchRepositories = new List<IAzureSearchRepository> { mockRepo.Object };

        var manager = new AzureDataMyWorkItemsManager(dataStore, mockAccountProvider.Object, mockLiveDataProvider.Object, mockConnectionProvider.Object, searchRepositories);

        var discovered = manager.DiscoverSearches().ToList();

        // Invalid URLs should be skipped
        Assert.AreEqual(0, discovered.Count);

        DataManagerTests.CleanUpDataStore(dataStore);
    }

    [TestMethod]
    public void TestDiscoverSearchesEmpty()
    {
        var dataStore = DataManagerTests.GetTestDataStore();
        var mockAccountProvider = new Mock<IAccountProvider>();
        var mockLiveDataProvider = new Mock<IAzureLiveDataProvider>();
        var mockConnectionProvider = new Mock<IConnectionProvider>();

        var searchRepositories = new List<IAzureSearchRepository>();

        var manager = new AzureDataMyWorkItemsManager(dataStore, mockAccountProvider.Object, mockLiveDataProvider.Object, mockConnectionProvider.Object, searchRepositories);

        var discovered = manager.DiscoverSearches().ToList();

        Assert.AreEqual(0, discovered.Count);

        DataManagerTests.CleanUpDataStore(dataStore);
    }
}
