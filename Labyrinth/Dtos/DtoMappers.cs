using Labyrinth.Crawl;
using Labyrinth.Items;

namespace Labyrinth.Dtos;

public static class DtoMappers
{
    public static Direction ToDomain(this DirectionEnum directionEnum)
    {
        return directionEnum switch
        {
            DirectionEnum.North => Direction.North,
            DirectionEnum.East => Direction.East,
            DirectionEnum.South => Direction.South,
            DirectionEnum.West => Direction.West,
            _ => throw new ArgumentOutOfRangeException(nameof(directionEnum), directionEnum, "Unknown direction")
        };
    }

    public static DirectionEnum ToDto(this Direction direction)
    {
        if (direction == Direction.North) return DirectionEnum.North;
        if (direction == Direction.East) return DirectionEnum.East;
        if (direction == Direction.South) return DirectionEnum.South;
        if (direction == Direction.West) return DirectionEnum.West;

        throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unknown direction");
    }

    public static ICollectable ToDomain(this InventoryItemDto dto)
    {
        return dto.Type switch
        {
            ItemType.Key => new Key(),
            _ => throw new ArgumentOutOfRangeException(nameof(dto.Type), dto.Type, "Unknown item type")
        };
    }

    public static InventoryItemDto ToDto(this ICollectable item, bool? moveRequired = null)
    {
        var itemType = item switch
        {
            Key => ItemType.Key,
            _ => throw new ArgumentOutOfRangeException(nameof(item), item.GetType(), "Unknown item type")
        };

        return new InventoryItemDto(itemType, moveRequired);
    }

    public static MyInventory ToDomainInventory(this IEnumerable<InventoryItemDto> dtos)
    {
        var inventory = new MyInventory();
        foreach (var dto in dtos)
        {
            var item = dto.ToDomain();
        }
        return inventory;
    }

    public static Task<IEnumerable<InventoryItemDto>> ToDtosAsync(this Inventory inventory)
    {
        return Task.FromResult<IEnumerable<InventoryItemDto>>(Array.Empty<InventoryItemDto>());
    }
}
