using System.Text.Json.Serialization;

namespace Labyrinth.Api.Models;


public record SettingsDto(
    [property: JsonPropertyName("random-seed")] int? RandomSeed,
    [property: JsonPropertyName("corridor-walls")] int[]? CorridorWalls,
    [property: JsonPropertyName("wall-doors")] int[][]? WallDoors,
    [property: JsonPropertyName("key-rooms")] int[][]? KeyRooms
);
