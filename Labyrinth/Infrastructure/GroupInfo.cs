using System.Text.Json.Serialization;

namespace Labyrinth.Infrastructure;

/// <summary>
/// DTO representing group information from the Syllab API.
/// </summary>
public record GroupInfoDto(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("app-keys")] int AppKeys,
    [property: JsonPropertyName("active-crawlers")] int ActiveCrawlers,
    [property: JsonPropertyName("api-calls")] long ApiCalls
);
