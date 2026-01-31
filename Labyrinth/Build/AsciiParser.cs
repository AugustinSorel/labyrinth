using Labyrinth.Tiles;

namespace Labyrinth.Build
{
    public class AsciiParser
    {
        public Tile[,] Parse(string ascii_map)
        {
            var lines = ascii_map.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            // Remove any leading/trailing empty lines
            while (lines.Count > 0 && string.IsNullOrEmpty(lines[0])) lines.RemoveAt(0);
            while (lines.Count > 0 && string.IsNullOrEmpty(lines[^1])) lines.RemoveAt(lines.Count - 1);

            if (lines.Count == 0) return new Tile[0,0];

            int maxWidth = lines.Max(l => l.Length);

            // Pad shorter lines with spaces to ensure rectangular map
            for (int i = 0; i < lines.Count; i++)
            {
                if (lines[i].Length < maxWidth)
                    lines[i] = lines[i].PadRight(maxWidth, ' ');
            }

            var width = maxWidth;
            var tiles = new Tile[width, lines.Count];
            
            using var km = new Keymaster();

            for (int y = 0; y < tiles.GetLength(1); y++)
            {
                for (int x = 0; x < tiles.GetLength(0); x++)
                {
                    tiles[x, y] = lines[y][x] switch
                    {
                        'x' => NewStartPos(x, y),
                        ' ' => new Room(),
                        '+' or '-' or '|' => Wall.Singleton,
                        '/' => km.NewDoor(),
                        'k' => km.NewKeyRoom(),
                        _ => throw new ArgumentException($"Invalid map: unknown character '{lines[y][x]}' at line {y}, col {x}.")
                    };
                }
            }
            return tiles;
        }
        public EventHandler<StartEventArgs>? StartPositionFound;

        private Room NewStartPos(int x, int y)
        {
            StartPositionFound?.Invoke(this, new StartEventArgs(x, y));
            return new Room();
        }
    }
}
