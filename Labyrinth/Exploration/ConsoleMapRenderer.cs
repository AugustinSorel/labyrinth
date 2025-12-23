using System;
using System.Linq;

namespace Labyrinth.Exploration
{
    public sealed class ConsoleMapRenderer
    {
        private readonly ExplorationMap _map;

        public ConsoleMapRenderer(ExplorationMap map)
        {
            _map = map;
        }

        public void Render(params (int X, int Y)[] crawlers)
        {
            if (!_map.Cells.Any())
                return;

            var minX = _map.Cells.Min(c => c.X);
            var maxX = _map.Cells.Max(c => c.X);
            var minY = _map.Cells.Min(c => c.Y);
            var maxY = _map.Cells.Max(c => c.Y);

            Console.Clear();

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (crawlers.Any(c => c.X == x && c.Y == y))
                    {
                        Console.Write('C');
                        continue;
                    }

                    var cell = _map.TryGet(x, y);

                    if (cell == null)
                    {
                        Console.Write('?');
                        continue;
                    }

                    Console.Write(cell.Type switch
                    {
                        KnownCellType.Wall => '#',
                        KnownCellType.Door => '/',
                        KnownCellType.Room => cell.Visited ? 'o' : '.',
                        _ => '?'
                    });
                }
                Console.WriteLine();
            }
        }
    }
}
