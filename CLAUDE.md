# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Command Palette Azure Extension (Preview) is a C# WinRT/WinUI 3 extension for PowerToys Command Palette that integrates Azure DevOps functionality. The extension allows users to access saved queries, pull request searches, and pipeline searches directly from Command Palette.

## Build and Development Commands

### Building the Project

**Build via Visual Studio:**
```powershell
# Open the solution in Visual Studio 2022 or later
start AzureExtension.sln
```

**Build via PowerShell script:**
```powershell
# Build for x64 Debug (default)
.\build\scripts\Build.ps1

# Build for specific platform and configuration
.\build\scripts\Build.ps1 -Platform x64 -Configuration Release

# Build for ARM64
.\build\scripts\Build.ps1 -Platform arm64 -Configuration Debug

# Build with version number
.\build\scripts\Build.ps1 -Platform x64 -Configuration Release -Version "1.0.0.0"

# View help
.\build\scripts\Build.ps1 -Help
```

Build outputs are placed in `BuildOutput/` directory as `.msix` packages.

**Note:** Certificate signing requires admin privileges. The build script will warn if not running as admin.

### Running Tests

**Run all tests:**
```powershell
# Via dotnet CLI (preferred for command line)
dotnet test AzureExtension.sln

# Run tests for specific platform
dotnet test AzureExtension.sln --configuration Debug --arch x64
dotnet test AzureExtension.sln --configuration Debug --arch arm64

# Run tests with detailed output
dotnet test AzureExtension.sln --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~AzureExtension.Test.Client.AzureUrlBuilderTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~AzureExtension.Test.Client.AzureUrlBuilderTests.TestMethod"
```

**Via Visual Studio:**
- Open Test Explorer (Test > Test Explorer)
- Build solution first
- Run All tests or select specific tests

**Test Project:** `AzureExtension.Test` uses MSTest framework with Moq for mocking. Tests run on both x64 and arm64 platforms.

### Installing Locally

1. Uninstall any existing version of the extension from Command Palette
2. Build the project
3. If running as admin, the package will be automatically signed
4. Install the `.msix` package from `BuildOutput/` directory

### Logging

Extension logs are located at:
```
%localappdata%\Packages\Microsoft.CmdPalAzureExtension_8wekyb3d8bbwe\TempState
```

## Architecture Overview

### Entry Point and COM Server

The extension runs as a COM server with `Program.cs` as the entry point:
- Single-instance application using `AppInstance`
- Handles COM, Protocol, and Launch activations
- Comprehensive Serilog logging setup
- Full dependency injection configuration

Main extension class: `AzureExtension.cs` (GUID: `23c9363b-1ade-4017-afa7-f57f0351bca1`)

### Command Palette Integration

**Command Provider:** `AzureExtensionCommandProvider` extends `CommandProvider`
- Provides top-level commands based on authentication state
- Event-driven UI updates via `RaiseItemsChanged()`
- Events: `OnLiveDataUpdate`, `OnSignInStatusChanged`, `OnSearchUpdated`

**Top-level commands:**
- When not authenticated: Sign In page
- When authenticated: Saved Queries, Saved Pull Request Searches, Saved Pipeline Searches, Sign Out, and any pinned searches

### Authentication System

**Components:**
- `AccountProvider` - MSAL-based token management
- `AuthenticationMediator` - Event bus for auth state changes
- Uses Windows Authentication Broker (WAM) for SSO
- Token cache backed by SQLite (MSAL extensions)

**Flow:**
1. Attempts SSO with Windows default account on first launch
2. Falls back to interactive authentication via `ShowLogonSession()`
3. Tokens cached and used for Azure DevOps API calls via `VssAadCredential`

### Azure DevOps API Integration

**Client Layer:**
- `AzureClientProvider` - Connection pooling by (Uri, IAccount) tuple
- `AzureLiveDataProvider` - Wraps Azure DevOps SDK clients:
  - `WorkItemTrackingHttpClient` - Queries and work items
  - `GitHttpClient` - Pull requests, repositories, commits
  - `BuildHttpClient` - Build definitions and builds
  - `PolicyHttpClient` - Policy evaluation records
  - `ProjectHttpClient` - Team projects
  - `ProfileHttpClient` - Avatar information

**URL Parsing:** `AzureUri` and `AzureUrlBuilder` classes parse and construct Azure DevOps URLs for queries, repositories, and pipeline definitions.

### Data Management and Caching

**Cache Manager (State Machine):**
- `CacheManager` with states: `IdleState`, `RefreshingState`, `PeriodicUpdatingState`, `PendingRefreshState`, `PendingClearCacheState`
- Periodic updates every 10 minutes
- Refresh cooldown: 3 minutes
- Events trigger UI updates via `OnLiveDataUpdate`

**Data Updaters:**
1. `AzureDataQueryManager` - Fetches work item query results (handles tree/flat/one-hop queries)
2. `AzureDataPullRequestSearchManager` - Fetches pull requests with criteria and policy evaluation
3. `AzureDataPipelineUpdater` - Fetches build definitions and builds

**Update Pipeline:**
```
CacheManager (State Machine)
    ↓
AzureDataManager (IDataUpdateService)
    ↓
IDataUpdater implementations (Query/PR/Pipeline managers)
```

### Data Model and Persistence

**Two SQLite databases:**
1. `AzureData.db` - Cache data (volatile, 7-day retention for work items)
2. `PersistentAzureData.db` - User searches and settings

**Core data objects:**
- `WorkItem` - Work item details (title, state, assignments, dates)
- `PullRequest` - PR details (title, status, policy status, creator, target branch)
- `Build` - Build run details (number, status, result, source branch)
- `Definition` - Pipeline definition with most recent build
- `Query` - Saved work item queries with metadata

**Schema hierarchy:**
```
Organization
    ↓
Project (1:N)
    ├─→ Query (1:N) → QueryWorkItem (1:N) → WorkItem
    ├─→ Repository (1:N) → PullRequest (1:N)
    └─→ Definition (1:N) → Build (1:N)

Supporting: Identity, WorkItemType, PullRequestSearch, PullRequestPolicyStatus, etc.
```

**Data access:** Dapper ORM with transaction support for consistency

### UI and Page System

**Page Hierarchy:**
- `SignInPage` / `SignOutPage` - Authentication
- `SavedQueriesPage`, `SavedPullRequestSearchesPage`, `SavedPipelineSearchesPage` - Main search management pages
- `SearchPages` - Dynamic pages from `SearchPageFactory` showing actual search results

**User workflow:**
1. User navigates to a "Saved Azure DevOps [type]" command
2. Selects "Add a [type]" to save a new search
3. Fills out form with URL, display name, and options
4. Search appears in saved list and can be pinned to top level
5. Selecting a saved search shows live results from Azure DevOps

### Key Design Patterns

- **Dependency Injection** - Full DI container in `Program.cs`
- **State Pattern** - `CacheManager` with state hierarchy
- **Observer Pattern** - Event-based UI and data notifications
- **Repository Pattern** - Saved searches persistence via `AzureSearchRepositoryAdapter`
- **Factory Pattern** - `SearchPageFactory` for dynamic page generation
- **Adapter Pattern** - Data provider and repository adapters
- **Connection Pooling** - `AzureClientProvider` caches connections

## Important Implementation Notes

### Azure DevOps API Constraints

- Work item batch fetches are limited to 200 items per API call (chunking required)
- Query types supported: Flat, Tree, OneHop
- Temporary queries (not saved in Azure DevOps) are not supported

### URL Format Requirements

The extension parses Azure DevOps URLs in specific formats:
- **Queries:** `https://dev.azure.com/{org}/{project}/_queries?id={queryId}`
- **Repositories (PR searches):** `https://dev.azure.com/{org}/{project}/_git/{repo}`
- **Pipeline Definitions:** `https://dev.azure.com/{org}/{project}/_build?definitionId={defId}`

Use `AzureUri` and `AzureUrlBuilder` classes for URL construction and parsing.

### Data Retention

- Work items cached for 7 days, then pruned
- Cache updates occur every 10 minutes in background
- User can manually refresh with 3-minute cooldown

### Testing Considerations

- Tests use `DataStoreTestsSetup` for database initialization
- `TestSetupHelpers` provides mock configuration utilities
- `TestContextSink` integrates Serilog with MSTest output
- Tests run on both x64 and ARM64 platforms
- Mock `IAzureLiveDataProvider` for unit tests to avoid real API calls

## Project Structure

```
AzureExtension/                    # Main extension project
├── Client/                        # Azure DevOps API client wrappers
├── Controls/                      # UI controls and pages
├── DataManager/                   # Cache manager and data updaters
├── DataModel/                     # Data objects and repository interfaces
│   └── DataObjects/              # Entity classes (WorkItem, PullRequest, Build, etc.)
├── DataStore/                     # SQLite data access layer (Dapper)
├── Helpers/                       # Utility classes
├── Providers/                     # Auth and service providers
├── Strings/                       # Localization resources
├── Widgets/                       # Widget implementations
└── AzureExtension.cs             # Main extension class

AzureExtension.Test/               # Test project (MSTest + Moq)
├── Client/                        # Client layer tests
├── DataManager/                   # Data management tests
├── DataStore/                     # Data object tests
├── Helpers/                       # Test utilities
└── Widgets/                       # UI component tests

build/                             # Build scripts and props
├── scripts/
│   ├── Build.ps1                 # Main build script
│   └── CertSignAndInstall.ps1    # Certificate signing
└── EnsureOutputLayout.props

docs/                              # Documentation
└── quickstartguide.md            # User quick start guide
```

## Dependencies

**Key NuGet packages:**
- `Microsoft.Identity.Client` 4.70.1-preview - MSAL authentication with WAM support
- `Microsoft.TeamFoundationServer.Client` 19.255.1 - Azure DevOps REST API clients
- `Microsoft.Data.Sqlite` 9.1.0 - SQLite database
- `Dapper` 2.1.35 - Micro ORM
- `Serilog` 4.0.1 - Logging framework

**Test packages:**
- `MSTest` 3.6.4 - Test framework
- `Moq` 4.20.72 - Mocking framework

## Development Workflow

1. **Before making changes:** Uninstall any existing Command Palette Azure Extension to avoid conflicts
2. **Making code changes:** Follow existing patterns (DI, async/await, event-driven updates)
3. **Testing:** Write unit tests in `AzureExtension.Test` mirroring the structure of main project
4. **Building:** Use `Build.ps1` script or Visual Studio
5. **Local testing:** Install the generated `.msix` package and test in Command Palette
6. **Debugging:** Check logs in `%localappdata%\Packages\Microsoft.CmdPalAzureExtension_8wekyb3d8bbwe\TempState`

## Common Patterns

### Adding a new data type

1. Create data object in `DataModel/DataObjects/`
2. Add database schema in `DataStore/DataStore.cs`
3. Implement `IDataUpdater` for fetching data from Azure DevOps
4. Register updater in `AzureDataManager`
5. Create page/command in `Controls/` to display data
6. Add to `AzureExtensionCommandProvider` top-level commands

### Adding a new Azure DevOps API call

1. Add method to `IAzureLiveDataProvider` interface
2. Implement in `AzureLiveDataProvider` using appropriate Azure DevOps SDK client
3. Call from data updater or command handler
4. Handle exceptions and log appropriately

### Event-driven UI updates

When data changes, raise events to trigger UI refresh:
```csharp
// In data updater or cache manager
OnLiveDataUpdate?.Invoke(this, EventArgs.Empty);

// In command provider
cacheManager.OnLiveDataUpdate += (s, e) => RaiseItemsChanged();
```
