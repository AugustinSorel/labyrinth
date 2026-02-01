using System.Text.Json.Serialization;

namespace Labyrinth.Dtos;

public record InventoryItemDto(
    [property: JsonPropertyName("type")] ItemType Type,
    [property: JsonPropertyName("move-required")] bool? MoveRequired = null
);
