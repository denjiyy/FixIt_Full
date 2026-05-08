# Test Failure Root Cause Analysis

## 1. EmailServiceTests.cs - Logger Verification Issue

**Test File**: [FixIt.Tests/Services/EmailServiceTests.cs](FixIt.Tests/Services/EmailServiceTests.cs)

**Issue**: ConsoleEmailServiceTests (lines 17-30) expect `Times.Once` but the service logs multiple times per method call.

**Root Cause**:
- `ConsoleEmailService.SendEmailAsync()` logs **3 times** (line 19-21 in ConsoleEmailService.cs):
  - To address
  - Subject
  - Body preview
- Methods like `SendHealthReportEmailAsync()` (line 27-29) call `SendEmailAsync()`, so they generate:
  1. One direct log call (line 27)
  2. Three log calls from `SendEmailAsync()` = **4 total** 

**Test Expectation vs Reality**:
- Test expects: `Times.Once` for each email method
- Service provides: Multiple log calls per method
  - `SendHealthReportEmailAsync` → 4 log calls
  - `SendWeeklyReminderEmailAsync` → 4 log calls
  - `SendHazardAlertEmailAsync` → 4 log calls

**What Needs Fixing**:
- Either consolidate logging to single call per method OR
- Update test expectations to `Times.AtLeastOnce` or specific count (like `Times.Exactly(4)`)

---

## 2. IssueServiceTests.cs - Status Transition Validation Failure

**Test File**: [FixIt.Tests/Services/IssueServiceTests.cs](FixIt.Tests/Services/IssueServiceTests.cs#L178-L191)

**Issue**: Test `UpdateIssueStatusAsync_WithValidStatus_UpdatesStatusSuccessfully` (lines 178-191) fails because it attempts invalid status transition.

**Service Implementation** ([IssueService.cs](FixIt.Services/IssueService.cs#L478-L495)):
```csharp
private static bool IsValidStatusTransition(IssueStatus from, IssueStatus to)
{
    var validTransitions = new Dictionary<IssueStatus, IssueStatus[]>
    {
        { IssueStatus.New, new[] { IssueStatus.Confirmed, IssueStatus.Rejected, IssueStatus.Duplicate } },
        { IssueStatus.Confirmed, new[] { IssueStatus.InProgress, IssueStatus.Rejected } },
        { IssueStatus.InProgress, new[] { IssueStatus.Fixed, IssueStatus.Rejected } },
        { IssueStatus.Fixed, new[] { IssueStatus.Archived } },
        { IssueStatus.Rejected, new[] { IssueStatus.Confirmed, IssueStatus.Archived } },
        { IssueStatus.Duplicate, new[] { IssueStatus.Archived } },
        { IssueStatus.Archived, Array.Empty<IssueStatus>() }
    };
    return validTransitions.TryGetValue(from, out var validNextStates) && validNextStates.Contains(to);
}
```

**Test Issue** (line 188):
```csharp
// Arranging issue with status New
var issue = new Issue { Id = "issue1", CityId = "city1", Status = IssueStatus.New, Title = "Test" };

// Attempting to transition to InProgress
await _issueService.UpdateIssueStatusAsync("issue1", IssueStatus.InProgress, "user1");
```

**Expected vs Actual**:
- Test expects: Can transition from `New` → `InProgress` ✗
- Service allows: `New` can only transition to `Confirmed`, `Rejected`, or `Duplicate` ✓
- Valid path: `New` → `Confirmed` → `InProgress`

**What Needs Fixing**:
- Test should transition `New` → `Confirmed` (valid first step), not directly to `InProgress`

---

## 3. HeatmapServiceTests.cs - Method Signature Mismatch

**Test File**: [FixIt.Tests/Services/HeatmapServiceTests.cs](FixIt.Tests/Services/HeatmapServiceTests.cs#L63-L79)

**Issue**: Test `GetIssueHotspots_ExcludesResolvedIssues` calls method with wrong signature.

**Test Code** (line 76):
```csharp
var result = await _heatmapService.GetIssueHotspots("city1");
```

**Service Implementation** ([HeatmapService.cs](FixIt.Services/Analytics/HeatmapService.cs#L50-L76)):
- Overload 1 (line 50): `public async Task<List<HeatmapLocationPoint>> GetIssueHotspots(string cityId, List<Issue> allIssues, int limit = 100)`
- Overload 2 (line 92): `public async Task<List<HeatmapLocationPoint>> GetIssueHotspots(string cityId, int limit = 100)`

**Expected vs Actual**:
- Test calls: `GetIssueHotspots(cityId)` without issues list
- Service provides: Overload 2 (line 92) handles this correctly
- **The test should work**, but the service's filtering logic needs verification:
  - Line 57-58 filters: `i.Status != IssueStatus.Fixed && i.Status != IssueStatus.Rejected`
  - But mock returns: issues with `IssueStatus.Fixed` status that should be excluded
  - The issue: Mock is returning all issues, and Overload 2 calls `GetIssueHotspots(cityId, issues.ToList(), limit)` which filters correctly

**What Needs Fixing**:
- Test setup is correct, but needs to verify the mock actually filters resolved issues
- Mock setup should reflect that `GetIssueHotspots` is called with the full list from the repo

---

## 4. ReputationServiceTests.cs - UserManager Mock Setup Issue

**Test File**: [FixIt.Tests/Services/ReputationServiceTests.cs](FixIt.Tests/Services/ReputationServiceTests.cs#L20-L35)

**Issue**: UserManager mock setup may fail due to how Identity framework expects UserManager to be constructed.

**Test Code** (lines 24-33):
```csharp
var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
_userManagerMock = new Mock<UserManager<ApplicationUser>>(
    It.IsAny<IUserStore<ApplicationUser>>(),
    It.IsAny<IOptions<IdentityOptions>>(),
    // ... more It.IsAny<> parameters
```

**Problem**:
- `It.IsAny<>()` in constructor arguments creates loose mock bindings
- Some methods in tests call `_userManager.FindByIdAsync(userId)` 
- Mock needs explicit setup for `FindByIdAsync` to return values
- Service code (line 224 in ReputationService.cs) calls: `var user = await _userManager.FindByIdAsync(userId);`

**Expected vs Actual**:
- Test expects: UserManager.FindByIdAsync returns user
- Service expects: Non-null ApplicationUser with properties like EmailConfirmed
- Default mock returns: null (not configured)

**Service Code Using FindByIdAsync** ([ReputationService.cs](FixIt.Services/Gamification/ReputationService.cs#L223-L224)):
```csharp
var user = await _userManager.FindByIdAsync(userId);
if (user?.EmailConfirmed == true && !existingAchievementTypes.Contains(AchievementType.VerifiedCitizen))
```

**What Needs Fixing**:
- Add setup for `_userManagerMock.Setup(m => m.FindByIdAsync(It.IsAny<string>())).ReturnsAsync((ApplicationUser?)null);`
- Or mock it to return a user when needed in specific tests

---

## 5. HazardServiceTests.cs - ResolveHazardAsync Authorization Check Failure

**Test File**: [FixIt.Tests/Services/HazardServiceTests.cs](FixIt.Tests/Services/HazardServiceTests.cs)

**Issue**: Test doesn't mock the authorization check that `ResolveHazardAsync` performs.

**Service Implementation** ([HazardService.cs](FixIt.Services/Safety/HazardService.cs#L180-L193)):
```csharp
public async Task<bool> ResolveHazardAsync(string hazardId, string userId, string? notes = null)
{
    if (string.IsNullOrWhiteSpace(userId))
        throw new UnauthorizedAccessException("Authenticated admin user is required to resolve hazards.");

    var user = await _userRepo.GetByIdAsync(userId);
    if (user == null || user.Role != UserRole.Admin)
        throw new UnauthorizedAccessException("Only administrators can resolve hazards.");
    // ... rest of method
}
```

**Expected vs Actual**:
- Service expects: Valid admin user to be returned from `_userRepo.GetByIdAsync(userId)`
- Test likely returns: null (not configured)
- Result: `UnauthorizedAccessException` thrown

**What Needs Fixing**:
- Test needs to mock `_userRepoMock.Setup(r => r.GetByIdAsync("user1")).ReturnsAsync(new ApplicationUser { Role = UserRole.Admin });`
- Or adjust test to use an admin user ID that's properly mocked

---

## 6. TagServiceTests.cs - IncrementUsageCountAsync Setup Issue

**Test File**: [FixIt.Tests/Services/TagServiceTests.cs](FixIt.Tests/Services/TagServiceTests.cs)

**Issue**: No test found for `IncrementUsageCountAsync` in the provided test code, but the service has this method.

**Service Implementation** ([TagService.cs](FixIt.Services/TagService.cs#L89-L95)):
```csharp
public async Task IncrementUsageCountAsync(string tagId)
{
    var tag = await _tagRepo.GetByIdAsync(tagId);
    if (tag != null)
    {
        tag.UsageCount++;
        tag.UpdatedAt = DateTime.UtcNow;
        await _tagRepo.ReplaceAsync(tagId, tag);
    }
}
```

**What Needs Fixing**:
- A test needs to be created that:
  1. Mocks `_tagRepoMock.Setup(r => r.GetByIdAsync("tag1")).ReturnsAsync(existingTag);`
  2. Calls `_tagService.IncrementUsageCountAsync("tag1");`
  3. Verifies `_tagRepoMock.Verify(r => r.ReplaceAsync("tag1", It.Is<Tag>(t => t.UsageCount == 1)), Times.Once);`

---

## 7. HealthReportServiceTests.cs - Engagement Metrics Calculation Issue

**Test File**: [FixIt.Tests/Services/HealthReportServiceTests.cs](FixIt.Tests/Services/HealthReportServiceTests.cs#L151-L173)

**Issue**: Test `GetCityHealthReportAsync_CountsEngagementMetrics` creates issues without proper status initialization.

**Test Code** (lines 165-166):
```csharp
new Issue { Id = "i1", CityId = "city1", CommentCount = 5, Upvotes = 10, Reporter = new UserSummary { Id = "u1" } },
new Issue { Id = "i2", CityId = "city1", CommentCount = 3, Upvotes = 7, Reporter = new UserSummary { Id = "u1" } }
```

**Service Implementation** ([HealthReportService.cs](FixIt.Services/Analytics/HealthReportService.cs#L68-L74)):
```csharp
report.TotalComments = issues.Sum(i => i.CommentCount);
report.TotalUpvotes = issues.Sum(i => i.Upvotes);
report.AverageUpvotesPerIssue = report.TotalIssues > 0 
    ? report.TotalUpvotes / report.TotalIssues 
    : 0;
```

**Expected vs Actual**:
- Test expects:
  - `TotalComments` = 8 (5 + 3)
  - `TotalUpvotes` = 17 (10 + 7)
  - `AverageUpvotesPerIssue` = 8.5 (17 / 2)
- Service provides: Correct calculation ✓
- **This should work if issues are properly counted**

**Problem**: Test issues don't have `Status` set, which might cause issues with the repository mock filter `i => i.CityId == cityId`

**What Needs Fixing**:
- Test issues should include `Status = IssueStatus.New` or appropriate status
- Verify mock repository returns the created issues correctly

---

## 8. MediaServiceTests.cs - File Validation & Mocking Issue

**Test File**: [FixIt.Tests/Services/MediaServiceTests.cs](FixIt.Tests/Services/MediaServiceTests.cs)

**Issue**: Tests mock IFormFile but the mock setup might not properly handle all stream operations.

**Service Implementation** ([MediaService.cs](FixIt.Services/MediaService.cs#L55-L80)):
```csharp
public async Task<Models.Media.Media> UploadFileAsync(
    IFormFile file, 
    string ownerId, 
    MediaReferenceType referenceType, 
    string referenceId)
{
    var (isValid, errorMessage) = ValidateFile(file);
    if (!isValid)
        throw new InvalidOperationException(errorMessage);
    
    await using var stream = file.OpenReadStream();
    await _fileStorage.SaveFileAsync(storagePath, stream);
}
```

**Test Mock Issue** (lines 52-59):
```csharp
private IFormFile CreateMockFile(string fileName, long fileSize, string contentType = "image/jpeg")
{
    var fileMock = new Mock<IFormFile>();
    var streamMock = new MemoryStream(Encoding.UTF8.GetBytes(new string('a', (int)fileSize)));
    
    fileMock.Setup(f => f.FileName).Returns(fileName);
    fileMock.Setup(f => f.Length).Returns(fileSize);
    fileMock.Setup(f => f.OpenReadStream()).Returns(streamMock);
```

**Problem**:
- Creating mock IFormFile as `fileMock.Object` returns a Moq proxy that doesn't fully implement IFormFile interface
- Some tests might fail because extension methods on IFormFile aren't properly mocked
- Stream is created once and can't be read twice (it's consumed after first read)

**What Needs Fixing**:
- Use `CallBase = true` on the mock or properly implement all IFormFile members
- Create a fresh MemoryStream for each read (use `Func<Stream>` instead of static stream)
- Test: Ensure MemoryStream is not exhausted after first read

---

## Summary Table

| Test File | Method | Issue | Impact | Fix Priority |
|-----------|--------|-------|--------|--------------|
| EmailServiceTests.cs | ConsoleEmailService.Send* | Logger called 3-4x per method, test expects Times.Once | Tests fail | HIGH |
| IssueServiceTests.cs | UpdateIssueStatusAsync | Invalid status transition (New→InProgress, should be New→Confirmed→InProgress) | Tests fail | HIGH |
| HeatmapServiceTests.cs | GetIssueHotspots | Method mock might not filter resolved issues correctly | Tests may fail | MEDIUM |
| ReputationServiceTests.cs | CheckAndAwardAchievementsAsync | UserManager.FindByIdAsync not mocked, returns null | Null reference exception | HIGH |
| HazardServiceTests.cs | ResolveHazardAsync | Admin check not mocked, returns null user | UnauthorizedAccessException | HIGH |
| TagServiceTests.cs | IncrementUsageCountAsync | No test exists for this method | Missing coverage | MEDIUM |
| HealthReportServiceTests.cs | GetCityHealthReportAsync | Test issues missing Status property | May fail filters | MEDIUM |
| MediaServiceTests.cs | UploadFileAsync | IFormFile mock stream exhausted after first read | Tests may fail | MEDIUM |
