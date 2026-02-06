using System.Text.Json.Serialization;

namespace Labyrinth.Infrastructure;

public record InventoryItemDto(
    [property: JsonPropertyName("type")] ItemType Type,
    [property: JsonPropertyName("move-required")] bool? MoveRequired = null
);
