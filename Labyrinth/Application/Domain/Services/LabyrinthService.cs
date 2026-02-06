using System.Net.Http.Json;
using Labyrinth.Infrastructure;

namespace Labyrinth.Application.Services;

public class LabyrinthService : ILabyrinthService
{
    private readonly HttpClient _httpClient;
    private readonly string _appKey;

    public LabyrinthService(HttpClient httpClient, string appKey)
    {
        _httpClient = httpClient;
        _appKey = appKey;
    }

    public async Task<IEnumerable<CrawlerDto>> GetCrawlersAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<IEnumerable<CrawlerDto>>($"/crawlers?appKey={_appKey}");
        return response ?? Array.Empty<CrawlerDto>();
    }

    public async Task<CrawlerDto> CreateCrawlerAsync(SettingsDto? settings = null)
    {
        var response = await _httpClient.PostAsJsonAsync($"/crawlers?appKey={_appKey}", settings);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CrawlerDto>() 
            ?? throw new InvalidOperationException("Failed to create crawler");
    }

    public async Task<CrawlerDto> GetCrawlerByIdAsync(Guid id)
    {
        var response = await _httpClient.GetFromJsonAsync<CrawlerDto>($"/crawlers/{id}?appKey={_appKey}");
        return response ?? throw new InvalidOperationException($"Crawler {id} not found");
    }

    public async Task<CrawlerDto> UpdateCrawlerAsync(Guid id, CrawlerDto crawler)
    {
        var response = await _httpClient.PatchAsJsonAsync($"/crawlers/{id}?appKey={_appKey}", crawler);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CrawlerDto>() 
            ?? throw new InvalidOperationException("Failed to update crawler");
    }

    public async Task DeleteCrawlerAsync(Guid id)
    {
        var response = await _httpClient.DeleteAsync($"/crawlers/{id}?appKey={_appKey}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<IEnumerable<InventoryItemDto>> GetBagAsync(Guid id)
    {
        var response = await _httpClient.GetFromJsonAsync<IEnumerable<InventoryItemDto>>($"/crawlers/{id}/bag?appKey={_appKey}");
        return response ?? Array.Empty<InventoryItemDto>();
    }

    public async Task<IEnumerable<InventoryItemDto>> UpdateBagAsync(Guid id, IEnumerable<InventoryItemDto> items)
    {
        var response = await _httpClient.PutAsJsonAsync($"/crawlers/{id}/bag?appKey={_appKey}", items);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IEnumerable<InventoryItemDto>>() 
            ?? Array.Empty<InventoryItemDto>();
    }

    public async Task<IEnumerable<InventoryItemDto>> GetItemsAsync(Guid id)
    {
        var response = await _httpClient.GetFromJsonAsync<IEnumerable<InventoryItemDto>>($"/crawlers/{id}/items?appKey={_appKey}");
        return response ?? Array.Empty<InventoryItemDto>();
    }

    public async Task<IEnumerable<InventoryItemDto>> UpdateItemsAsync(Guid id, IEnumerable<InventoryItemDto> items)
    {
        var response = await _httpClient.PutAsJsonAsync($"/crawlers/{id}/items?appKey={_appKey}", items);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IEnumerable<InventoryItemDto>>() 
            ?? Array.Empty<InventoryItemDto>();
    }

    public async Task<IEnumerable<GroupInfoDto>> GetGroupsAsync()
    {
        var response = await _httpClient.GetFromJsonAsync<IEnumerable<GroupInfoDto>>("/Groups");
        return response ?? Array.Empty<GroupInfoDto>();
    }
}
