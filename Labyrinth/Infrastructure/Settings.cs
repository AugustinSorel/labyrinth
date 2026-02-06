using System.Text.Json.Serialization;

namespace Labyrinth.Infrastructure;

public record SettingsDto(
    [property: JsonPropertyName("random-seed")] int? RandomSeed = null,
    [property: JsonPropertyName("corridor-walls")] IReadOnlyList<int>? CorridorWalls = null,
    [property: JsonPropertyName("wall-doors")] IReadOnlyList<IReadOnlyList<int>>? WallDoors = null,
    [property: JsonPropertyName("key-rooms")] IReadOnlyList<IReadOnlyList<int>>? KeyRooms = null
);
