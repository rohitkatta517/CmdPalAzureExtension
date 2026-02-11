// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AzureExtension.Data;
using AzureExtension.DataModel;
using Dapper;
using Dapper.Contrib.Extensions;
using Microsoft.TeamFoundation.Build.WebApi;
using Microsoft.TeamFoundation.Core.WebApi;

namespace AzureExtension.Test;

[TestClass]
public class DefinitionTests
{
    public TestContext? TestContext { get; set; }

    private TestOptions _testOptions = new();

    private TestOptions TestOptions
    {
        get => _testOptions;
        set => _testOptions = value;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestOptions = TestHelpers.SetupTempTestOptions(TestContext!);
        TestHelpers.ConfigureTestLog(TestOptions, TestContext!);
    }

    [TestCleanup]
    public void Cleanup()
    {
        TestHelpers.CloseTestLog();
        TestHelpers.CleanupTempTestOptions(TestOptions, TestContext!);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AddOrUpdate_ShouldNotUpdate_WhenWithinThreshold()
    {
        // Arrange: Create a data store and add an initial definition
        using var dataStore = new DataStore("TestStore", TestHelpers.GetDataStoreFilePath(TestOptions), TestOptions.DataStoreOptions.DataStoreSchema!);
        Assert.IsNotNull(dataStore);
        dataStore.Create();
        Assert.IsNotNull(dataStore.Connection);

        using var tx = dataStore.Connection.BeginTransaction();
        var org = Organization.GetOrCreate(dataStore, new Uri("https://dev.azure.com/testorg/"));
        Assert.IsNotNull(org);
        var project = new Project { Name = "TestProject", InternalId = "1", OrganizationId = org.Id };
        dataStore.Connection.Insert(project);
        tx.Commit();

        // Create first definition
        var testProject = new TeamProjectReference { Name = "TestProject" };
        var defRef1 = new DefinitionReference
        {
            Id = 123,
            Name = "Test Pipeline",
            CreatedDate = DateTime.UtcNow.AddDays(-1),
            Url = "https://dev.azure.com/testorg/TestProject/_apis/build/Definitions/123",
            Project = testProject,
        };

        var definition1 = Definition.GetOrCreate(dataStore, defRef1, project.Id);
        var originalTimeUpdated = definition1.TimeUpdated;
        TestContext?.WriteLine($"Original definition created with TimeUpdated: {originalTimeUpdated}");

        // Act: Try to update the same definition shortly after (within 4 hour threshold)
        // Simulate time passing but less than threshold
        Thread.Sleep(100); // Small delay to ensure different timestamps
        var defRef2 = new DefinitionReference
        {
            Id = 123,
            Name = "Test Pipeline Updated",
            CreatedDate = DateTime.UtcNow.AddDays(-1),
            Url = "https://dev.azure.com/testorg/TestProject/_apis/build/Definitions/123",
            Project = testProject,
        };

        var definition2 = Definition.GetOrCreate(dataStore, defRef2, project.Id);

        // Assert: Should return existing definition without updating
        Assert.AreEqual(definition1.Id, definition2.Id);
        Assert.AreEqual(originalTimeUpdated, definition2.TimeUpdated, "TimeUpdated should not change when within threshold");
        TestContext?.WriteLine($"Second definition has TimeUpdated: {definition2.TimeUpdated}");
        TestContext?.WriteLine("Definition was correctly NOT updated (within threshold)");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AddOrUpdate_ShouldUpdate_WhenExceedsThreshold()
    {
        // Arrange: Create a data store and add an initial definition with old timestamp
        using var dataStore = new DataStore("TestStore", TestHelpers.GetDataStoreFilePath(TestOptions), TestOptions.DataStoreOptions.DataStoreSchema!);
        Assert.IsNotNull(dataStore);
        dataStore.Create();
        Assert.IsNotNull(dataStore.Connection);

        using var tx = dataStore.Connection.BeginTransaction();
        var org = Organization.GetOrCreate(dataStore, new Uri("https://dev.azure.com/testorg/"));
        Assert.IsNotNull(org);
        var project = new Project { Name = "TestProject", InternalId = "1", OrganizationId = org.Id };
        dataStore.Connection.Insert(project);
        tx.Commit();

        // Create first definition
        var testProject = new TeamProjectReference { Name = "TestProject" };
        var defRef1 = new DefinitionReference
        {
            Id = 456,
            Name = "Old Pipeline",
            CreatedDate = DateTime.UtcNow.AddDays(-1),
            Url = "https://dev.azure.com/testorg/TestProject/_apis/build/Definitions/456",
            Project = testProject,
        };

        var definition1 = Definition.GetOrCreate(dataStore, defRef1, project.Id);
        var originalId = definition1.Id;
        TestContext?.WriteLine($"Original definition ID: {originalId}, TimeUpdated: {definition1.TimeUpdated}");

        // Manually update the TimeUpdated to simulate old data (5 hours ago, beyond 4 hour threshold)
        var oldTime = DateTime.UtcNow.AddHours(-5).Ticks;
        var sql = "UPDATE Definition SET TimeUpdated = @TimeUpdated WHERE Id = @Id";
        dataStore.Connection.Execute(sql, new { TimeUpdated = oldTime, Id = originalId });

        // Verify the update worked
        var definitionCheck = Definition.GetByInternalId(dataStore, 456);
        Assert.IsNotNull(definitionCheck);
        Assert.AreEqual(oldTime, definitionCheck.TimeUpdated);
        TestContext?.WriteLine($"Updated definition to have old timestamp: {oldTime}");

        // Act: Try to update with new data (should succeed since threshold exceeded)
        var defRef2 = new DefinitionReference
        {
            Id = 456,
            Name = "Updated Pipeline",
            CreatedDate = DateTime.UtcNow.AddDays(-1),
            Url = "https://dev.azure.com/testorg/TestProject/_apis/build/Definitions/456",
            Project = testProject,
        };

        var definition2 = Definition.GetOrCreate(dataStore, defRef2, project.Id);

        // Assert: Should update the definition since threshold was exceeded
        Assert.AreEqual(originalId, definition2.Id, "Should reuse same ID");
        Assert.IsTrue(definition2.TimeUpdated > oldTime, "TimeUpdated should be newer than old time");
        TestContext?.WriteLine($"Updated definition TimeUpdated: {definition2.TimeUpdated}");
        TestContext?.WriteLine("Definition was correctly UPDATED (exceeded threshold)");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetByInternalId_ShouldReturnDefinition_WhenExists()
    {
        // Arrange
        using var dataStore = new DataStore("TestStore", TestHelpers.GetDataStoreFilePath(TestOptions), TestOptions.DataStoreOptions.DataStoreSchema!);
        Assert.IsNotNull(dataStore);
        dataStore.Create();
        Assert.IsNotNull(dataStore.Connection);

        using var tx = dataStore.Connection.BeginTransaction();
        var org = Organization.GetOrCreate(dataStore, new Uri("https://dev.azure.com/testorg/"));
        Assert.IsNotNull(org);
        var project = new Project { Name = "TestProject", InternalId = "1", OrganizationId = org.Id };
        dataStore.Connection.Insert(project);
        tx.Commit();

        var testProject = new TeamProjectReference { Name = "TestProject" };
        var defRef = new DefinitionReference
        {
            Id = 789,
            Name = "Test Pipeline",
            CreatedDate = DateTime.UtcNow,
            Url = "https://dev.azure.com/testorg/TestProject/_apis/build/Definitions/789",
            Project = testProject,
        };

        Definition.GetOrCreate(dataStore, defRef, project.Id);

        // Act
        var result = Definition.GetByInternalId(dataStore, 789);

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual(789, result.InternalId);
        Assert.AreEqual("Test Pipeline", result.Name);
        TestContext?.WriteLine($"Successfully retrieved definition by internal ID: {result.InternalId}");
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void GetByInternalId_ShouldReturnNull_WhenNotExists()
    {
        // Arrange
        using var dataStore = new DataStore("TestStore", TestHelpers.GetDataStoreFilePath(TestOptions), TestOptions.DataStoreOptions.DataStoreSchema!);
        Assert.IsNotNull(dataStore);
        dataStore.Create();

        // Act
        var result = Definition.GetByInternalId(dataStore, 999);

        // Assert
        Assert.IsNull(result);
        TestContext?.WriteLine("Correctly returned null for non-existent definition");
    }
}
