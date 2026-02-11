# Command Palette Azure Extension - Feature Roadmap

## Context

The current extension provides basic Azure DevOps integration: saved queries (work items), saved PR searches, and saved pipeline views. However, for a developer on the Lumos team at Microsoft (ADO org: `office`, project: `OC`), the daily workflow spans far beyond ADO -- Kusto telemetry, ICM incidents, Geneva monitoring, EV2 deployments, eng.ms docs, and ADO wiki.

**Goal**: Transform this extension from a "saved search viewer" into a **developer command center** -- a single entry point that mirrors the developer's actual daily workflow, reducing browser round-trips and context switching.

---

## Feature Roadmap (User-Prioritized)

### Phase 1: My Work & Team Board
*"What am I working on today?"*

#### F1. My Active Work Items (Zero-Config)
- **What**: Top-level command showing work items assigned to the current user in non-Closed states. Works immediately after sign-in -- no saved query needed.
- **How**: Uses `WorkItemTrackingHttpClient.QueryByWiqlAsync` with WIQL: `[System.AssignedTo] = @Me AND [System.State] <> 'Closed' AND [System.State] <> 'Removed'`
- **Scope**: Medium (~1 week)
- **Status**: In Progress
- **Key changes**:
  - Add `QueryByWiqlAsync` to `IAzureLiveDataProvider` / `AzureLiveDataProvider`
  - New `MyWorkItemsSearch` class (built-in search, no URL input needed)
  - New `AzureDataMyWorkItemsManager : IDataUpdater` (generates WIQL at runtime)
  - Reuse existing `WorkItemsSearchPage` for rendering
  - Add to `AzureExtensionCommandProvider.TopLevelCommands()`
  - Wire in `Program.cs`
- **Key files**:
  - `AzureExtension\Client\IAzureLiveDataProvider.cs`
  - `AzureExtension\DataManager\Managers\AzureDataQueryManager.cs` (pattern to follow)
  - `AzureExtension\Controls\SearchPages\WorkItemsSearchPage.cs` (reuse)
  - `AzureExtension\AzureExtensionCommandProvider.cs`
  - `AzureExtension\Program.cs`

#### F2. Team Kanban Board Quick Launch
- **What**: Top-level deep link to open the team's Kanban board in browser. Constructed from saved org/project data.
- **URL pattern**: `https://dev.azure.com/{org}/{project}/_boards/board/t/{team}/Backlog%20items`
- **Scope**: Small (~1-2 days)
- **Status**: Not Started
- **Key changes**:
  - Extend `AzureUrlBuilder` with board/backlog URL patterns
  - Add as a top-level command using existing `LinkCommand`
  - Could include sub-links: Board, Backlog, Sprints, Queries hub
- **Key files**:
  - `AzureExtension\Client\AzureUrlBuilder.cs`
  - `AzureExtension\AzureExtensionCommandProvider.cs`

---

### Phase 2: My PRs (Rich Status)
*"What's the status of my code changes?"*

#### F3. My PRs with Rich Inline Status
- **What**: Top-level command showing PRs created by the current user with rich inline status badges:
  - **Approvals**: `2/3 approved` (count of approvals vs required reviewers)
  - **Comments**: `5 active` (count of unresolved comment threads)
  - **Build**: `Passing` / `Failing` / `Running` (PR build policy status)
- **Inline badges visible at a glance in the list**, plus click to see full detail view
- **Scope**: Medium-Large (~1.5 weeks)
- **Status**: Not Started
- **How**:
  - Use `GitHttpClient.GetPullRequestsAsync` with `searchCriteria.CreatorId = currentUser` for the PR list
  - Use `GitHttpClient.GetThreadsAsync` to get comment threads and count active (unresolved) ones
  - Use existing `PolicyHttpClient.GetPolicyEvaluationsAsync` for build status (already implemented)
  - Use `GitHttpClient.GetPullRequestReviewersAsync` for approval count and vote breakdown
- **Key changes**:
  - Add `GetPullRequestThreadsAsync`, `GetPullRequestReviewersAsync` to `IAzureLiveDataProvider`
  - New `MyPRsSearch` (built-in, zero-config)
  - Extend `PullRequest` data object or create enriched DTO with: `ApprovalCount`, `RequiredReviewerCount`, `ActiveCommentCount`, `BuildStatus`
  - Extend `PullRequestSearchPage.GetListItem()` to render inline badges (using Tags or subtitle text)
  - Detail view: show reviewer list with vote status, thread summary, build details
- **Key files**:
  - `AzureExtension\Client\IAzureLiveDataProvider.cs`
  - `AzureExtension\DataManager\Managers\AzureDataPullRequestSearchManager.cs`
  - `AzureExtension\DataModel\DataObjects\PullRequest.cs`
  - `AzureExtension\Controls\SearchPages\PullRequestSearchPage.cs`

---

### Phase 3: Team PRs
*"What does my team need from me?"*

#### F4. Team PR Overview
- **What**: Show all active PRs across the team's repos. Three built-in views:
  1. **PRs needing my review** -- PRs where I'm a reviewer (existing `PullRequestView.Assigned` path)
  2. **All active PRs** -- All open PRs in saved repos (existing `PullRequestView.All` path)
  3. **My PRs across all repos** -- My created PRs across all saved repos (complement of F3)
- Same rich inline status as F3 (approvals, comments, build)
- **Scope**: Medium (~1 week) -- mostly reuses F3 infrastructure
- **Status**: Not Started
- **Key changes**:
  - Create `TeamPRsSearch` implementing multiple filter views
  - Reuse F3's enriched PR rendering
  - Add filter toggle in the page (Mine / Needs My Review / All)
- **Key files**:
  - `AzureExtension\Controls\SearchPages\PullRequestSearchPage.cs`
  - `AzureExtension\AzureExtensionCommandProvider.cs`

---

### Phase 4: Search Across ADO
*"Where is that code / doc / article?"*

#### F5. ADO Code Search
- **What**: Search code across repositories using Azure DevOps Search API. Results show file name, path, repo, and matching snippet. Click to open in browser.
- **Scope**: Medium-Large (~2 weeks)
- **Status**: Not Started
- **How**: ADO Search API endpoint: `POST https://almsearch.dev.azure.com/{org}/{project}/_apis/search/codesearchresults`
- **Key considerations**:
  - Search API may need `Microsoft.VisualStudio.Services.Search.Shared.WebApi` NuGet package, or direct REST calls via `HttpClient`
  - This is a **live query** (not cached) -- user types search term, results appear
  - Need a text input page pattern (different from existing saved-search list pages)
- **Key changes**:
  - Add `SearchCodeAsync` to `IAzureLiveDataProvider` (may need direct REST call if SDK doesn't include it)
  - New `CodeSearchPage` with text input + results list
  - Each result: file icon + path + snippet + link to file in ADO web
- **Key files**:
  - `AzureExtension\Client\IAzureLiveDataProvider.cs` or new `AzureSearchClient`
  - New `AzureExtension\Controls\Pages\CodeSearchPage.cs`

#### F6. ADO Wiki Search
- **What**: Search ADO wiki pages. Results show page title, path, and snippet. Click to open in browser.
- **Scope**: Medium (~1-1.5 weeks)
- **Status**: Not Started
- **How**: ADO Search API: `POST https://almsearch.dev.azure.com/{org}/{project}/_apis/search/wikisearchresults`
- **Pattern**: Same live-query pattern as F5. Could share a `SearchPage` base class.
- **Key changes**:
  - Add `SearchWikiAsync` to `IAzureLiveDataProvider`
  - New `WikiSearchPage` (or extend shared `SearchPage` from F5)
- **Key files**: Same pattern as F5

#### F7. Eng.ms / Engineering Hub Search
- **What**: Search eng.ms documentation. Open results in browser.
- **Scope**: Small-Medium (~3-5 days)
- **Status**: Not Started
- **How**: Two approaches:
  1. **Simple**: Construct eng.ms search URL with query and open in browser (`https://eng.ms/search?query={term}`)
  2. **Richer**: If eng.ms has a search API, fetch results inline (needs investigation)
- **Recommendation**: Start with approach 1 (URL launcher) -- immediate value, zero complexity. Add inline results later if API is available.
- **Key changes**:
  - Add "Search Eng Hub" command that takes text input and opens the search URL
  - Minimal code: text input form + `LinkCommand` with constructed URL

---

### Phase 5: Geneva Dashboards with Filters
*"How is my service doing right now?"*

#### F8. Geneva Dashboard Launcher with Pre-Configured Filters
- **What**: Saved Geneva dashboard bookmarks with configurable filter presets. Each bookmark can have pre-configured filter values for:
  - **Time range**: Last 1h, 4h, 24h, 7d
  - **Environment**: INT, PPE, PROD
  - **Partner ID**: Specific Dime partner IDs
  - **Dime Activity**: ShowDime, PreloadDime, LoadConfig, etc.
- **How**: Geneva/Jarvis dashboard URLs support query parameters for filters. Save the base dashboard URL + filter combinations as presets.
- **Example**: A single dashboard might have 3 saved presets:
  - "Dime Health - PROD Last 1h" -> base URL + `?time=1h&env=prod`
  - "Dime Health - PPE Last 24h" -> base URL + `?time=24h&env=ppe`
  - "ShowDime Errors - PROD" -> base URL + `?time=4h&env=prod&activity=ShowDime`
- **Scope**: Small-Medium (~3-5 days)
- **Status**: Not Started
- **UI Design**:
  - "Saved Dashboards" top-level command
  - "Add a dashboard" form: URL, display name, category
  - "Add a preset" sub-form: name + filter key-value pairs appended to URL
  - Each saved dashboard shows its presets as sub-items
  - Pin frequently used presets to top-level
- **Implementation pattern**: Extends the existing saved-search/bookmark pattern
  - New `DashboardBookmark` table in `PersistentAzureData.db` with fields: `Name`, `BaseUrl`, `Category`, `IsTopLevel`
  - New `DashboardPreset` table: `BookmarkId`, `PresetName`, `FilterParams` (URL query string)
  - `SavedDashboardsPage`, `SaveDashboardForm`, `DashboardPresetListItem`
- **Key files**:
  - `AzureExtension\PersistentData\PersistentDataSchema.cs`
  - New repository following `PullRequestSearchRepository` pattern
  - New pages following `SavedPullRequestSearchesPage` pattern

---

### Phase 6: Future Features (Lower Priority)

| Feature | Description | Scope |
|---------|-------------|-------|
| Kusto Quick Launcher | Saved cluster/database bookmarks, open in ADX web | Small (~3 days) |
| Pipeline Trigger/Rerun | One-click rerun failed builds | Small (~3-5 days) |
| Quick PR Actions | Vote approve/reject from CmdPal | Medium (~1 week) |
| Work Item State Transitions | Change state from context menu | Small (~3-5 days) |
| Recent Items History | Track recently opened items | Medium (~1 week) |
| ICM Incident Viewer | Active incidents for team | Large (~2-3 weeks) |
| Create Work Item | Quick creation form | Medium (~1-2 weeks) |
| Daily Standup Helper | Composite view of all my items | Medium (~1 week) |
| Real-Time Notifications | Toast for PR reviews, build failures | Large (~3 weeks) |

---

## Suggested Build Order

| # | Feature | Est. Effort | Why This Order |
|---|---------|-------------|---------------|
| 1 | **F1 - My Active Work Items** | ~1 week | First thing you check every day |
| 2 | **F2 - Team Kanban Board Link** | ~1-2 days | Quick win, pairs with F1 |
| 3 | **F3 - My PRs (Rich Status)** | ~1.5 weeks | Core daily workflow, builds PR infrastructure |
| 4 | **F4 - Team PRs** | ~1 week | Reuses F3 infrastructure |
| 5 | **F5 - ADO Code Search** | ~2 weeks | New search pattern, high daily frequency |
| 6 | **F6 - ADO Wiki Search** | ~1-1.5 weeks | Reuses F5 search pattern |
| 7 | **F7 - Eng Hub Search** | ~3-5 days | URL launcher, simple |
| 8 | **F8 - Geneva Dashboards** | ~3-5 days | Bookmark pattern, high ops value |

---

## Technical Architecture Notes

### New Patterns Introduced

1. **Built-in Search (F1, F3, F4)**: Searches that auto-configure from signed-in user identity. No URL input needed. New `IBuiltInSearch` interface extending `IAzureSearch`.

2. **Live Query Pages (F5, F6)**: Pages with text input that query APIs in real-time (not cached). New `LiveSearchPage<T>` base class with debounced input handling.

3. **Dashboard Bookmarks with Presets (F8)**: Parameterized URL templates with saved filter combinations. New persistence tables and forms.

### Key Files Modified Across Features
- `AzureExtension\Client\IAzureLiveDataProvider.cs` -- New API methods (WIQL, threads, reviewers, search)
- `AzureExtension\Client\AzureLiveDataProvider.cs` -- Implementations
- `AzureExtension\AzureExtensionCommandProvider.cs` -- New top-level commands
- `AzureExtension\Program.cs` -- DI wiring
- `AzureExtension\Client\AzureUrlBuilder.cs` -- Deep link URL patterns
- `AzureExtension\PersistentData\PersistentDataSchema.cs` -- New tables

### Auth Considerations
- **F1-F7**: All use existing ADO auth tokens. No new auth setup.
- **F8**: Pure URL launcher. No auth from the extension.
- **Future ICM/EV2/ECS**: Would need new AAD scopes.

### Starting Point
We should start with **F1 (My Active Work Items)** as it:
- Provides immediate daily value
- Introduces the "built-in search" pattern reused by F3 and F4
- Has the lowest risk (extends existing work item infrastructure)
- Is completely self-contained (no dependencies on other features)

## Verification
For each feature, test by:
1. Build with `.\build\scripts\Build.ps1 -Platform x64`
2. Run tests with `.\build\scripts\Test.ps1 -Platform x64`
3. Install the `.msix` from `BuildOutput/` and test in Command Palette
4. Verify feature works with actual ADO data (office/OC project)
