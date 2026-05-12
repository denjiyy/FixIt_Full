using System.Net.Http.Json;
using FixIt.Mobile.Models;

namespace FixIt.Mobile.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;

    public ApiService(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("FixItApi");
    }

    public async Task<List<Issue>> GetIssuesAsync()
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/issues");
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var issues = await response.Content.ReadFromJsonAsync<List<Issue>>();
            return issues ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        try
        {
            var payload = new
            {
                Email = email,
                Password = password
            };

            using var response = await _httpClient.PostAsJsonAsync("api/auth/login", payload);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ReportIssueAsync(string title, string description, Stream photo)
    {
        try
        {
            using var multipartContent = new MultipartFormDataContent();
            multipartContent.Add(new StringContent(title), "title");
            multipartContent.Add(new StringContent(description), "description");

            if (photo != Stream.Null)
            {
                if (photo.CanSeek)
                {
                    photo.Position = 0;
                }

                var streamContent = new StreamContent(photo);
                streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                multipartContent.Add(streamContent, "photo", "issue-photo.jpg");
            }

            using var response = await _httpClient.PostAsync("api/issues", multipartContent);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
