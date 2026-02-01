using System.Text.Json.Serialization;
using Labyrinth.Crawl;
using Labyrinth.Tiles;

namespace Labyrinth.Dtos;

public record CrawlerDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("direction")]
    [property: JsonConverter(typeof(JsonStringEnumConverter))] DirectionEnum Direction,
    [property: JsonPropertyName("walking")] bool Walking,
    [property: JsonPropertyName("facing-tile")]
    [property: JsonConverter(typeof(JsonStringEnumConverter))] TileType FacingTile,
    [property: JsonPropertyName("bag")] IReadOnlyList<InventoryItemDto>? Bag = null,
    [property: JsonPropertyName("items")] IReadOnlyList<InventoryItemDto>? Items = null
);

public enum DirectionEnum
{
    North,
    East,
    South,
    West
}
