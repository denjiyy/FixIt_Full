using System.Net;
using System.Net.Http.Json;
using MongoDB.Bson;
using Xunit;

namespace FixIt.Tests.Integration;

/// <summary>
/// End-to-end coverage for the core issue lifecycle over the real HTTP pipeline
/// (WebApplicationFactory + Mongo testcontainer): create → fetch → vote →
/// comment → list comments, plus the unauthenticated and validation gates.
/// Exercises routing, model binding/validation, DI, the services, and JSON
/// serialization together — the layers unit tests stub out.
/// </summary>
public class IssuesLifecycleIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public IssuesLifecycleIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    private void RequireDocker() => Skip.IfNot(_fixture.IsAvailable,
        $"Docker testcontainer unavailable. {_fixture.UnavailabilityReason ?? "(no reason captured)"}");

    [SkippableFact]
    public async Task CreateIssue_AsAuthenticatedUser_IsFetchableById()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();

        var (issueId, _) = await CreateIssueAsync(client, title: "Broken streetlight on Cherni Vrah");

        var get = await client.GetAsync($"/api/issues/{issueId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var issue = await ReadDataAsync<IssueDto>(get);
        Assert.Equal(issueId, issue.Id);
        Assert.Equal("Broken streetlight on Cherni Vrah", issue.Title);
    }

    [SkippableFact]
    public async Task CreateIssue_WithoutAuthentication_Returns401()
    {
        RequireDocker();
        var client = _fixture.Factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var response = await client.PostAsJsonAsync("/api/issues", new
        {
            title = "Anonymous attempt",
            description = "This issue should be rejected because the caller is not authenticated.",
            latitude = 42.6977,
            longitude = 23.3219,
            cityId = ObjectId.GenerateNewId().ToString(),
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task CreateIssue_WithTooShortTitle_Returns400()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/issues", new
        {
            title = "ab", // below the 3-char minimum
            description = "A valid description that easily clears the ten character minimum.",
            latitude = 42.6977,
            longitude = 23.3219,
            cityId = ObjectId.GenerateNewId().ToString(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task VoteOnIssue_ByAnotherUser_IncrementsUpvotes()
    {
        RequireDocker();
        var (_, author) = await NewUserClientAsync();
        var (issueId, initialUpvotes) = await CreateIssueAsync(author);

        var (_, voter) = await NewUserClientAsync();
        var response = await voter.PostAsJsonAsync($"/api/issues/{issueId}/vote", new { voteType = 1 }); // VoteType.Up

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadDataAsync<VoteResultDto>(response);
        Assert.Equal(initialUpvotes + 1, result.Upvotes);
        Assert.Equal(0, result.Downvotes);
    }

    [SkippableFact]
    public async Task CommentOnIssue_ThenList_ReturnsTheComment()
    {
        RequireDocker();
        var (_, client) = await NewUserClientAsync();
        var (issueId, _) = await CreateIssueAsync(client);

        var add = await client.PostAsJsonAsync($"/api/issues/{issueId}/comments", new
        {
            text = "I drive past this every morning — confirmed still broken.",
        });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var created = await ReadDataAsync<CommentDto>(add);
        Assert.Equal("I drive past this every morning — confirmed still broken.", created.Text);

        var list = await client.GetAsync($"/api/issues/{issueId}/comments");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var comments = await ReadDataAsync<List<CommentDto>>(list);
        Assert.Contains(comments, c => c.Id == created.Id);
    }

    // ---- helpers ----

    private async Task<(string Email, HttpClient Client)> NewUserClientAsync()
    {
        var (email, password) = await _fixture.ProvisionRegularUserAsync(
            email: $"issue-user-{Guid.NewGuid():N}@fixit.test");
        var (_, client) = await _fixture.LoginAndGetClientAsync(email, password);
        return (email, client);
    }

    private static async Task<(string IssueId, int Upvotes)> CreateIssueAsync(
        HttpClient client, string title = "Large pothole on Vitosha Blvd")
    {
        var response = await client.PostAsJsonAsync("/api/issues", new
        {
            title,
            description = "Deep pothole near the tram stop that keeps growing after the rain.",
            latitude = 42.6977,
            longitude = 23.3219,
            cityId = ObjectId.GenerateNewId().ToString(),
            address = "Vitosha Blvd 1",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var issue = await ReadDataAsync<IssueDto>(response);
        return (issue.Id, issue.Upvotes);
    }

    private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
    {
        var envelope = await response.Content.ReadFromJsonAsync<IntegrationTestFixture.ApiEnvelope<T>>()
            ?? throw new InvalidOperationException("Response body was not a parseable ApiResponse envelope.");
        Assert.True(envelope.Success, $"Expected a success envelope but got: {envelope.Message}");
        return envelope.Data ?? throw new InvalidOperationException("Envelope contained no data.");
    }

    private sealed record IssueDto(string Id, string Title, string Description, int Upvotes, int Downvotes, int CommentCount);

    private sealed record VoteResultDto(int Upvotes, int Downvotes);

    private sealed record CommentDto(string Id, string IssueId, string Text);
}
