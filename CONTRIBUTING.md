# Contributing to FixIt

Thank you for your interest in contributing to FixIt! This document provides guidelines and instructions for contributing.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Workflow](#development-workflow)
- [Coding Standards](#coding-standards)
- [Testing Guidelines](#testing-guidelines)
- [Pull Request Guidelines](#pull-request-guidelines)
- [Documentation](#documentation)

---

## Code of Conduct

- Be respectful and inclusive
- Focus on constructive feedback
- Welcome newcomers and help them learn
- Keep discussions professional and on-topic

---

## Getting Started

### 1. Fork and Clone

```bash
git clone https://github.com/your-username/FixIt.git
cd FixIt
```

### 2. Set Up Development Environment

See [README.md](README.md#getting-started) for setup instructions.

### 3. Create a Branch

```bash
git checkout -b feature/your-feature-name
# or
git checkout -b fix/issue-123
```

**Branch naming conventions:**
- `feature/` - New features
- `fix/` - Bug fixes
- `docs/` - Documentation changes
- `refactor/` - Code refactoring
- `test/` - Test additions or modifications
- `chore/` - Maintenance tasks

---

## Development Workflow

### Making Changes

1. **Make small, focused commits** - Each commit should address a single concern
2. **Write meaningful commit messages** - See [Commit Message Guidelines](#commit-message-guidelines)
3. **Run tests frequently** - Ensure your changes don't break existing functionality
4. **Update documentation** - Add XML docs for public APIs, update README if needed

### Before Submitting

- [ ] Code compiles without errors or warnings
- [ ] All tests pass (`dotnet test`)
- [ ] Code follows [Coding Standards](#coding-standards)
- [ ] New features have tests
- [ ] Documentation is updated
- [ ] No sensitive data in code or commits (API keys, passwords, etc.)

---

## Coding Standards

### C# Conventions

```csharp
// Use nullable reference types (enabled by default)
public string? GetUserName(int userId) => ...

// PascalCase for public members
public class UserService { }
public void ProcessIssue() { }

// camelCase for private members
private readonly IRepository _repository;
private int _counter;

// Use var when type is obvious
var users = await _repository.FindAsync(u => u.IsActive);

// Use expression-bodied members for simple methods
public int GetCount() => _repository.Count();

// Prefer pattern matching
if (user is { IsBanned: true, BannedReason: not null })
{
    // Handle banned user
}

// Use async/await properly
public async Task<User> GetUserAsync(int id)
{
    return await _repository.GetByIdAsync(id);
}

// Avoid async void (except for event handlers)
// ❌ Bad
public async void ProcessData() { }

// ✓ Good
public async Task ProcessDataAsync() { }
```

### Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Classes | PascalCase | `IssueService`, `ApplicationUser` |
| Interfaces | PascalCase with I prefix | `IRepository`, `IMediaService` |
| Methods | PascalCase | `GetIssueAsync`, `CreateUser` |
| Properties | PascalCase | `DisplayName`, `CreatedAt` |
| Private fields | camelCase with underscore | `_repository`, `_logger` |
| Local variables | camelCase | `user`, `issueCount` |
| Constants | PascalCase | `MaxFileSize`, `DefaultPageSize` |
| Enums | PascalCase | `IssueStatus`, `UserRole` |

### File Organization

- One class per file (with exceptions for small related types)
- Named matching the class: `IssueService.cs` contains `IssueService`
- Organize class members:
  1. Public properties
  2. Constructor
  3. Public methods
  4. Protected methods
  5. Private methods
  6. Private fields

### Dependency Injection

```csharp
// ✓ Good - Constructor injection
public class IssueService : IIssueService
{
    private readonly IRepository<Issue> _repository;
    private readonly ILogger<IssueService> _logger;

    public IssueService(
        IRepository<Issue> repository,
        ILogger<IssueService> logger)
    {
        _repository = repository;
        _logger = logger;
    }
}
```

### Error Handling

```csharp
// ✓ Good - Specific exceptions with context
if (issue is null)
{
    throw new NotFoundException($"Issue with ID '{id}' not found");
}

// ✓ Good - Catch specific exceptions
try
{
    await _repository.DeleteAsync(id);
}
catch (MongoException ex)
{
    _logger.LogError(ex, "Failed to delete issue {IssueId}", id);
    throw new DataException($"Database error while deleting issue {id}", ex);
}

// ❌ Bad - Empty catch blocks
try { ... } catch { }
```

---

## Testing Guidelines

### Test Structure

```csharp
public class IssueServiceTests
{
    private readonly Mock<IRepository<Issue>> _mockRepository;
    private readonly IssueService _service;

    public IssueServiceTests()
    {
        _mockRepository = new Mock<IRepository<Issue>>();
        _service = new IssueService(_mockRepository.Object);
    }

    [Fact]
    public async Task GetIssueById_ExistingIssue_ReturnsIssue()
    {
        // Arrange
        var issueId = ObjectId.GenerateNewId().ToString();
        var expectedIssue = new Issue { Id = issueId, Title = "Test" };
        _mockRepository
            .Setup(r => r.GetByIdAsync(issueId))
            .ReturnsAsync(expectedIssue);

        // Act
        var result = await _service.GetIssueByIdAsync(issueId);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Test");
    }

    [Fact]
    public async Task GetIssueById_NonExistent_ThrowsNotFoundException()
    {
        // Arrange
        var issueId = ObjectId.GenerateNewId().ToString();
        _mockRepository.Setup(r => r.GetByIdAsync(issueId)).ReturnsAsync((Issue)null);

        // Act & Assert
        await FluentActions
            .Invoking(() => _service.GetIssueByIdAsync(issueId))
            .Should()
            .ThrowAsync<NotFoundException>();
    }
}
```

### Test Naming

- Use descriptive names: `Method_Scenario_ExpectedResult`
- Example: `CreateIssue_InvalidLocation_ThrowsValidationException`

### Test Principles

- **FIRST**: Fast, Independent, Repeatable, Self-validating, Timely
- One assertion per concept (multiple asserts OK if testing same behavior)
- Use [FluentAssertions](https://fluentassertions.com/) for readability
- Mock external dependencies

---

## Pull Request Guidelines

### PR Title

- Clear and concise
- Prefix with type: `feat:`, `fix:`, `docs:`, `refactor:`, `test:`, `chore:`
- Example: `feat: Add heatmap visualization for issue clusters`

### PR Description

```markdown
## Summary
Brief description of changes

## Changes
- Added X
- Modified Y
- Removed Z

## Testing
- [ ] Unit tests added/updated
- [ ] Manual testing performed
- [ ] No breaking changes

## Screenshots (if UI changes)
Before: ...
After: ...

## Related Issues
Closes #123
```

### Review Process

1. Submit PR with clear description
2. Wait for automated checks (CI)
3. Maintainer reviews
4. Address feedback
5. Approval and merge

---

## Commit Message Guidelines

### Format

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Types

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation
- `style`: Formatting (no code changes)
- `refactor`: Code refactoring
- `test`: Tests
- `chore`: Maintenance

### Examples

```
feat(issues): Add voting endpoint to issues API

Implemented upvote/downvote functionality with:
- POST /api/issues/{id}/vote
- DELETE /api/issues/{id}/vote
- Vote weight based on user trust level

Closes #45

feat(ai): Integrate OpenAI for issue analysis

- Automatic categorization
- Duplicate detection
- Priority suggestions

BREAKING CHANGE: Issue.Create now requires Category field

fix(auth): Resolve JWT token expiration issue

- Changed token expiration from 24h to 30m
- Added refresh token rotation

Fixes #78

docs(readme): Update installation instructions

- Added MongoDB setup steps
- Clarified environment variables
```

---

## Documentation

### Code Comments

```csharp
/// <summary>
/// Creates a new issue report with the specified details.
/// </summary>
/// <param name="request">The issue creation request containing title, description, and location.</param>
/// <param name="userId">The ID of the user creating the issue.</param>
/// <returns>The created issue with generated ID and initial status.</returns>
/// <exception cref="ValidationException">Thrown when the request data is invalid.</exception>
/// <exception cref="UnauthorizedException">Thrown when the user is not authenticated.</exception>
public async Task<Issue> CreateIssueAsync(CreateIssueRequest request, string userId)
{
    // Implementation
}
```

### README Updates

Update README.md when:
- Adding new features
- Changing configuration options
- Modifying API endpoints
- Adding/changing requirements

---

## Questions?

- Check existing [issues](https://github.com/denjiyy/FixIt/issues)
- Read the [codebase documentation](README.md)
- Ask in discussions

Thank you for contributing to FixIt!
