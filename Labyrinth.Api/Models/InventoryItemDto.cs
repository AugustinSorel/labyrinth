using System.Text.Json.Serialization;

namespace Labyrinth.Api.Models;


public record InventoryItemDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("move-required")] bool? MoveRequired = null
);
