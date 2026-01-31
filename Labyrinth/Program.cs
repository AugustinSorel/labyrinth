using Labyrinth;
using Labyrinth.Crawl;
using Labyrinth.Sys;

if (args.Length < 2)
{
    Console.WriteLine("Usage: labyrinth <server_address> <api_key>");
    Environment.Exit(1);
}

Configuration.ServerAddress = args[0];
Configuration.ApiKey = args[1];

char DirToChar(Direction dir) =>
    "^<v>"[dir.DeltaX * dir.DeltaX + dir.DeltaX + dir.DeltaY + 1];

void DrawExplorer(object? sender, CrawlingEventArgs e)
{
    Console.SetCursorPosition(e.X, e.Y);
    Console.Write(DirToChar(e.Direction));
    Console.SetCursorPosition(0, 0);
    Thread.Sleep(500);
}

var labyrinth = new Labyrinth.Maze("""
    +--+--------+
    |  /        |
    |  +--+--+  |
    |     |k    |
    +--+  |  +--+
       |k  x    |
    +  +-------/|
    |           |
    +-----------+
    """);
var crawler = labyrinth.NewCrawler();
var prevX = crawler.X;
var prevY = crawler.Y;
var explorer = new RandExplorer(
    crawler, 
    new BasicEnumRandomizer<RandExplorer.Actions>()
);

explorer.DirectionChanged += DrawExplorer;
explorer.PositionChanged  += (s, e) =>
{
    Console.SetCursorPosition(prevX, prevY);
    Console.Write(' ');
    DrawExplorer(s, e);
    (prevX, prevY) = (e.X, e.Y);
};

Console.Clear();
Console.WriteLine(labyrinth);
await explorer.GetOutAsync(1000);
