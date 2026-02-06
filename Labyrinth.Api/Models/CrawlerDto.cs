using System.Text.Json.Serialization;

namespace Labyrinth.Api.Models;

public record CrawlerDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("direction")] int Direction,
    [property: JsonPropertyName("is-walking")] bool IsWalking,
    [property: JsonPropertyName("app-key")] Guid AppKey
);
