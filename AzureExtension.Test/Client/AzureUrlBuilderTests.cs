// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Client;

namespace AzureExtension.Test.Client;

[TestClass]
public class AzureUrlBuilderTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void ExtractComponents_ModernUrl_ReturnsCorrectComponents()
    {
        var apiUrl = "https://dev.azure.com/myorg/myproject/_apis/build/Builds/12345";
        var components = AzureUrlBuilder.ExtractComponents(apiUrl);

        Assert.IsTrue(components.IsValid);
        Assert.AreEqual(AzureHostType.Modern, components.HostType);
        Assert.AreEqual("myorg", components.Organization);
        Assert.AreEqual("https://dev.azure.com", components.BaseUrl);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ExtractComponents_LegacyUrl_ReturnsCorrectComponents()
    {
        var apiUrl = "https://myorg.visualstudio.com/myproject/_apis/build/Builds/12345";
        var components = AzureUrlBuilder.ExtractComponents(apiUrl);

        Assert.IsTrue(components.IsValid);
        Assert.AreEqual(AzureHostType.Legacy, components.HostType);
        Assert.AreEqual("myorg", components.Organization);
        Assert.AreEqual("https://myorg.visualstudio.com", components.BaseUrl);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ExtractComponents_OnPremUrl_ReturnsCorrectComponents()
    {
        var apiUrl = "https://myserver.company.com/DefaultCollection/myproject/_apis/build/Builds/12345";
        var components = AzureUrlBuilder.ExtractComponents(apiUrl);

        Assert.IsTrue(components.IsValid);
        Assert.AreEqual(AzureHostType.NotHosted, components.HostType);
        Assert.AreEqual("https://myserver.company.com", components.BaseUrl);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ExtractComponents_NullUrl_ReturnsInvalidComponents()
    {
        var components = AzureUrlBuilder.ExtractComponents((string)null!);
        Assert.IsFalse(components.IsValid);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ExtractComponents_EmptyUrl_ReturnsInvalidComponents()
    {
        var components = AzureUrlBuilder.ExtractComponents(string.Empty);
        Assert.IsFalse(components.IsValid);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ExtractComponents_InvalidUrl_ReturnsInvalidComponents()
    {
        var components = AzureUrlBuilder.ExtractComponents("not-a-valid-url");
        Assert.IsFalse(components.IsValid);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildBuildResultsUrl_ModernUrl_ReturnsCorrectHtmlUrl()
    {
        var apiUrl = "https://dev.azure.com/myorg/myproject/_apis/build/Builds/12345";
        var projectName = "MyProject";
        var buildId = 12345L;

        var result = AzureUrlBuilder.BuildBuildResultsUrl(apiUrl, projectName, buildId);

        Assert.AreEqual("https://dev.azure.com/myorg/MyProject/_build/results?buildId=12345&view=results", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildBuildResultsUrl_LegacyUrl_ReturnsCorrectHtmlUrl()
    {
        var apiUrl = "https://myorg.visualstudio.com/myproject/_apis/build/Builds/12345";
        var projectName = "MyProject";
        var buildId = 12345L;

        var result = AzureUrlBuilder.BuildBuildResultsUrl(apiUrl, projectName, buildId);

        Assert.AreEqual("https://myorg.visualstudio.com/MyProject/_build/results?buildId=12345&view=results", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildBuildResultsUrl_OnPremUrl_ReturnsCorrectHtmlUrl()
    {
        var apiUrl = "https://myserver.company.com/DefaultCollection/myproject/_apis/build/Builds/12345";
        var projectName = "MyProject";
        var buildId = 12345L;

        var result = AzureUrlBuilder.BuildBuildResultsUrl(apiUrl, projectName, buildId);

        Assert.AreEqual("https://myserver.company.com/MyProject/_build/results?buildId=12345&view=results", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [ExpectedException(typeof(ArgumentException))]
    public void BuildBuildResultsUrl_NullUrl_ThrowsArgumentException()
    {
        AzureUrlBuilder.BuildBuildResultsUrl(null!, "project", 123);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [ExpectedException(typeof(ArgumentException))]
    public void BuildBuildResultsUrl_EmptyUrl_ThrowsArgumentException()
    {
        AzureUrlBuilder.BuildBuildResultsUrl(string.Empty, "project", 123);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildDefinitionUrl_ModernUrl_ReturnsCorrectHtmlUrl()
    {
        var apiUrl = "https://dev.azure.com/myorg/myproject/_apis/build/Definitions/42";
        var projectName = "MyProject";
        var definitionId = 42L;

        var result = AzureUrlBuilder.BuildDefinitionUrl(apiUrl, projectName, definitionId);

        Assert.AreEqual("https://dev.azure.com/myorg/MyProject/_build?definitionId=42", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildDefinitionUrl_LegacyUrl_ReturnsCorrectHtmlUrl()
    {
        var apiUrl = "https://myorg.visualstudio.com/myproject/_apis/build/Definitions/42";
        var projectName = "MyProject";
        var definitionId = 42L;

        var result = AzureUrlBuilder.BuildDefinitionUrl(apiUrl, projectName, definitionId);

        Assert.AreEqual("https://myorg.visualstudio.com/MyProject/_build?definitionId=42", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildDefinitionUrl_OnPremUrl_ReturnsCorrectHtmlUrl()
    {
        var apiUrl = "https://myserver.company.com/DefaultCollection/myproject/_apis/build/Definitions/42";
        var projectName = "MyProject";
        var definitionId = 42L;

        var result = AzureUrlBuilder.BuildDefinitionUrl(apiUrl, projectName, definitionId);

        Assert.AreEqual("https://myserver.company.com/MyProject/_build?definitionId=42", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [ExpectedException(typeof(ArgumentException))]
    public void BuildDefinitionUrl_NullUrl_ThrowsArgumentException()
    {
        AzureUrlBuilder.BuildDefinitionUrl(null!, "project", 42);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildPullRequestUrl_ValidCloneUrl_ReturnsCorrectHtmlUrl()
    {
        var cloneUrl = "https://dev.azure.com/myorg/myproject/_git/myrepo";
        var pullRequestId = 123;

        var result = AzureUrlBuilder.BuildPullRequestUrl(cloneUrl, pullRequestId);

        Assert.AreEqual("https://dev.azure.com/myorg/myproject/_git/myrepo/pullrequest/123", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildPullRequestUrl_CloneUrlWithTrailingSlash_ReturnsCorrectHtmlUrl()
    {
        var cloneUrl = "https://dev.azure.com/myorg/myproject/_git/myrepo/";
        var pullRequestId = 456;

        var result = AzureUrlBuilder.BuildPullRequestUrl(cloneUrl, pullRequestId);

        Assert.AreEqual("https://dev.azure.com/myorg/myproject/_git/myrepo/pullrequest/456", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildPullRequestUrl_LegacyCloneUrl_ReturnsCorrectHtmlUrl()
    {
        var cloneUrl = "https://myorg.visualstudio.com/myproject/_git/myrepo";
        var pullRequestId = 789;

        var result = AzureUrlBuilder.BuildPullRequestUrl(cloneUrl, pullRequestId);

        Assert.AreEqual("https://myorg.visualstudio.com/myproject/_git/myrepo/pullrequest/789", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    [ExpectedException(typeof(ArgumentException))]
    public void BuildPullRequestUrl_NullUrl_ThrowsArgumentException()
    {
        AzureUrlBuilder.BuildPullRequestUrl(null!, 123);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildWorkItemUrl_ModernComponents_ReturnsCorrectHtmlUrl()
    {
        var components = new AzureUrlBuilder.AzureUrlComponents(
            AzureHostType.Modern,
            "myorg",
            "https://dev.azure.com");
        var projectName = "MyProject";
        var workItemId = 12345;

        var result = AzureUrlBuilder.BuildWorkItemUrl(components, projectName, workItemId);

        Assert.AreEqual("https://dev.azure.com/myorg/MyProject/_workitems/edit/12345", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildWorkItemUrl_LegacyComponents_ReturnsCorrectHtmlUrl()
    {
        var components = new AzureUrlBuilder.AzureUrlComponents(
            AzureHostType.Legacy,
            "myorg",
            "https://myorg.visualstudio.com");
        var projectName = "MyProject";
        var workItemId = 12345;

        var result = AzureUrlBuilder.BuildWorkItemUrl(components, projectName, workItemId);

        Assert.AreEqual("https://myorg.visualstudio.com/MyProject/_workitems/edit/12345", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildQueryUrl_ModernComponents_ReturnsCorrectHtmlUrl()
    {
        var components = new AzureUrlBuilder.AzureUrlComponents(
            AzureHostType.Modern,
            "myorg",
            "https://dev.azure.com");
        var projectName = "MyProject";
        var queryId = "12345678-1234-1234-1234-123456789012";

        var result = AzureUrlBuilder.BuildQueryUrl(components, projectName, queryId);

        Assert.AreEqual("https://dev.azure.com/myorg/MyProject/_queries/query/12345678-1234-1234-1234-123456789012", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildQueryUrl_LegacyComponents_ReturnsCorrectHtmlUrl()
    {
        var components = new AzureUrlBuilder.AzureUrlComponents(
            AzureHostType.Legacy,
            "myorg",
            "https://myorg.visualstudio.com");
        var projectName = "MyProject";
        var queryId = "12345678-1234-1234-1234-123456789012";

        var result = AzureUrlBuilder.BuildQueryUrl(components, projectName, queryId);

        Assert.AreEqual("https://myorg.visualstudio.com/MyProject/_queries/query/12345678-1234-1234-1234-123456789012", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildRepositoryUrl_ModernComponents_ReturnsCorrectHtmlUrl()
    {
        var components = new AzureUrlBuilder.AzureUrlComponents(
            AzureHostType.Modern,
            "myorg",
            "https://dev.azure.com");
        var projectName = "MyProject";
        var repositoryName = "MyRepo";

        var result = AzureUrlBuilder.BuildRepositoryUrl(components, projectName, repositoryName);

        Assert.AreEqual("https://dev.azure.com/myorg/MyProject/_git/MyRepo", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildRepositoryUrl_LegacyComponents_ReturnsCorrectHtmlUrl()
    {
        var components = new AzureUrlBuilder.AzureUrlComponents(
            AzureHostType.Legacy,
            "myorg",
            "https://myorg.visualstudio.com");
        var projectName = "MyProject";
        var repositoryName = "MyRepo";

        var result = AzureUrlBuilder.BuildRepositoryUrl(components, projectName, repositoryName);

        Assert.AreEqual("https://myorg.visualstudio.com/MyProject/_git/MyRepo", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ExtractComponents_FromAzureUri_ModernUrl_ReturnsCorrectComponents()
    {
        var azureUri = new AzureUri("https://dev.azure.com/myorg/myproject/_git/myrepo");
        var components = AzureUrlBuilder.ExtractComponents(azureUri);

        Assert.IsTrue(components.IsValid);
        Assert.AreEqual(AzureHostType.Modern, components.HostType);
        Assert.AreEqual("myorg", components.Organization);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ExtractComponents_FromAzureUri_LegacyUrl_ReturnsCorrectComponents()
    {
        var azureUri = new AzureUri("https://myorg.visualstudio.com/myproject/_git/myrepo");
        var components = AzureUrlBuilder.ExtractComponents(azureUri);

        Assert.IsTrue(components.IsValid);
        Assert.AreEqual(AzureHostType.Legacy, components.HostType);
        Assert.AreEqual("myorg", components.Organization);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ExtractComponents_FromInvalidAzureUri_ReturnsInvalidComponents()
    {
        var azureUri = new AzureUri((string)null!);
        var components = AzureUrlBuilder.ExtractComponents(azureUri);
        Assert.IsFalse(components.IsValid);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildBuildResultsUrl_LargeBuildId_ReturnsCorrectHtmlUrl()
    {
        var apiUrl = "https://dev.azure.com/myorg/myproject/_apis/build/Builds/999999999";
        var projectName = "MyProject";
        var buildId = 999999999L;

        var result = AzureUrlBuilder.BuildBuildResultsUrl(apiUrl, projectName, buildId);

        Assert.AreEqual("https://dev.azure.com/myorg/MyProject/_build/results?buildId=999999999&view=results", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildBuildResultsUrl_ProjectNameWithSpaces_ReturnsCorrectHtmlUrl()
    {
        var apiUrl = "https://dev.azure.com/myorg/my%20project/_apis/build/Builds/12345";
        var projectName = "My Project";
        var buildId = 12345L;

        var result = AzureUrlBuilder.BuildBuildResultsUrl(apiUrl, projectName, buildId);

        Assert.AreEqual("https://dev.azure.com/myorg/My Project/_build/results?buildId=12345&view=results", result);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void BuildDefinitionUrl_ProjectNameWithSpaces_ReturnsCorrectHtmlUrl()
    {
        var apiUrl = "https://myorg.visualstudio.com/my%20project/_apis/build/Definitions/42";
        var projectName = "My Project";
        var definitionId = 42L;

        var result = AzureUrlBuilder.BuildDefinitionUrl(apiUrl, projectName, definitionId);

        Assert.AreEqual("https://myorg.visualstudio.com/My Project/_build?definitionId=42", result);
    }
}
