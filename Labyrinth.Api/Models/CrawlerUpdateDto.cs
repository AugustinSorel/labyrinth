using System.Text.Json.Serialization;

namespace Labyrinth.Api.Models;


public record CrawlerUpdateDto(
    [property: JsonPropertyName("direction")] int? Direction = null,
    [property: JsonPropertyName("is-walking")] bool? IsWalking = null
);
