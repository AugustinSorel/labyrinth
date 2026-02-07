using System.Net.Http.Json;
using Labyrinth.Items;
using Labyrinth.Tiles;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Labyrinth.Crawl
{
    public class ApiCrawler : ICrawler, IAsyncDisposable
    {
        private readonly HttpClient _http;
        private readonly string _crawlerId;
        private readonly string _appKey;
        private Direction _dir = Direction.North;
        private int _x;
        private int _y;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        private static int DirectionToInt(Direction d) => d switch
        {
            var x when x.Equals(Direction.North) => 0,
            var x when x.Equals(Direction.East) => 1,
            var x when x.Equals(Direction.South) => 2,
            var x when x.Equals(Direction.West) => 3,
            _ => 0
        };

        private static Direction IntToDirection(int i) => i switch
        {
            0 => Direction.North,
            1 => Direction.East,
            2 => Direction.South,
            3 => Direction.West,
            _ => Direction.North
        };

        private static Direction ParseDirection(string? s, Direction fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            if (int.TryParse(s, out var i)) return IntToDirection(i);
            return s switch
            {
                "North" => Direction.North,
                "East" => Direction.East,
                "South" => Direction.South,
                "West" => Direction.West,
                _ => fallback
            };
        }

        private ApiCrawler(HttpClient http, string crawlerId, string appKey)
        {
            _http = http;
            _crawlerId = crawlerId;
            _appKey = appKey;
        }

        public static async Task<ApiCrawler> CreateAsync(string baseUrl, string appKey, string? ascii = null)
        {
            var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

            var urlWithQuery = $"/crawlers?appKey={Uri.EscapeDataString(appKey)}";
            HttpResponseMessage res = null!;

            try
            {
                res = await http.PostAsJsonAsync(urlWithQuery, (object?)null);

                if (!res.IsSuccessStatusCode)
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, "/crawlers");
                    request.Content = JsonContent.Create((object?)null);
                    request.Headers.Add("X-App-Key", appKey);

                    res = await http.SendAsync(request);
                    if (!res.IsSuccessStatusCode)
                    {
                        var request2 = new HttpRequestMessage(HttpMethod.Post, "/crawlers");
                        request2.Content = JsonContent.Create((object?)null);
                        request2.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", appKey);

                        res = await http.SendAsync(request2);
                        if (!res.IsSuccessStatusCode)
                        {
                            var content = await ReadResponseContentSafeAsync(res);
                            throw new HttpRequestException($"Failed to create crawler. Status: {res.StatusCode}. Response: {content}");
                        }
                    }
                }
            }
            catch (HttpRequestException) { throw; }
            catch (Exception ex)
            {
                http.Dispose();
                throw new InvalidOperationException("Error while creating remote crawler: " + ex.Message, ex);
            }

            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            string id = json.GetProperty("id").GetString() ?? throw new InvalidOperationException("No crawler id returned");
            var client = new ApiCrawler(http, id, appKey);

            try
            {
                var state = await client.GetStateAsync();
                if (state != null)
                {
                    client._x = state.GetX() ?? client._x;
                    client._y = state.GetY() ?? client._y;
                    client._dir = ParseDirection(state.Direction, Direction.North);
                }
            }
            catch { }

            return client;
        }

        private static async Task<string?> ReadResponseContentSafeAsync(HttpResponseMessage res)
        {
            try { return await res.Content.ReadAsStringAsync(); }
            catch { return null; }
        }

        public int X => _x;
        public int Y => _y;
        public Direction Direction => _dir;

        private class InventoryItemDto
        {
            [JsonPropertyName("type")] public string? Type { get; set; }
            [JsonPropertyName("move-required")] public bool? MoveRequired { get; set; }
            [JsonPropertyName("move_required")] public bool? MoveRequiredAlt { get; set; }
        }

        private class CrawlerDto
        {
            [JsonPropertyName("id")] public string? Id { get; set; }
            [JsonPropertyName("x")] public JsonElement X { get; set; }
            [JsonPropertyName("y")] public JsonElement Y { get; set; }
            [JsonPropertyName("direction")] public string? Direction { get; set; }
            [JsonPropertyName("facing-tile")] public string? FacingTile { get; set; }
            [JsonPropertyName("facing")] public string? Facing { get; set; }
            [JsonPropertyName("items")] public InventoryItemDto[]? Items { get; set; }
            [JsonPropertyName("bag")] public InventoryItemDto[]? Bag { get; set; }

            public int? GetInt(JsonElement e)
            {
                try
                {
                    if (e.ValueKind == JsonValueKind.Number) return e.GetInt32();
                    if (e.ValueKind == JsonValueKind.String) return int.Parse(e.GetString() ?? "0");
                }
                catch { }
                return null;
            }

            public int? GetX() => GetInt(X);
            public int? GetY() => GetInt(Y);
            public string? GetFacing() => FacingTile ?? Facing;
        }

        private async Task<CrawlerDto?> GetStateAsync()
        {
            var res = await _http.GetAsync($"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            if (!res.IsSuccessStatusCode) return null;

            try
            {
                var content = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<CrawlerDto>(content, _jsonOptions);
            }
            catch { return null; }
        }

        public async Task<TileType> GetFacingTileTypeAsync()
        {
            var state = await GetStateAsync();
            if (state == null) return TileType.Empty;
            var facing = state.GetFacing() ?? "Empty";
            
            return facing switch
            {
                "Wall" => TileType.Wall,
                "Door" => TileType.Door,
                "Outside" => TileType.Outside,
                "Exit" => TileType.Outside,
                "Exterior" => TileType.Outside,
                "Escape" => TileType.Outside,
                "Room" => TileType.Empty,
                "Empty" => TileType.Empty,
                _ => TileType.Empty,
            };
        }

        public bool CanMoveForward
        {
            get
            {
                try
                {
                    var res = _http.GetAsync($"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}").GetAwaiter().GetResult();
                    if (!res.IsSuccessStatusCode) return false;
                    var dto = JsonSerializer.Deserialize<CrawlerDto>(res.Content.ReadAsStringAsync().GetAwaiter().GetResult(), _jsonOptions);
                    var facing = dto?.GetFacing() ?? "Empty";
                    return facing switch
                    {
                        "Wall" => false,
                        "Door" => false,
                        "Outside" => false,
                        _ => true,
                    };
                }
                catch { return false; }
            }
        }

        public void TurnRight()
        {
            var newDirInt = _dir switch
            {
                var d when d.Equals(Direction.North) => 1,
                var d when d.Equals(Direction.East) => 2,
                var d when d.Equals(Direction.South) => 3,
                _ => 0,
            };

            var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            req.Content = JsonContent.Create(new { id = _crawlerId, x = _x, y = _y, direction = newDirInt, walking = false }, options: _jsonOptions);
            var res = _http.SendAsync(req).GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode) return;
            _dir = IntToDirection(newDirInt);
        }

        public void TurnLeft()
        {
            var newDirInt = _dir switch
            {
                var d when d.Equals(Direction.North) => 3,
                var d when d.Equals(Direction.West) => 2,
                var d when d.Equals(Direction.South) => 1,
                _ => 0,
            };

            var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            req.Content = JsonContent.Create(new { id = _crawlerId, x = _x, y = _y, direction = newDirInt, walking = false }, options: _jsonOptions);
            var res = _http.SendAsync(req).GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode) return;
            _dir = IntToDirection(newDirInt);
        }

        public async Task<bool> TryUnlockAsync(Inventory keyChain)
        {
            var state = await GetStateAsync();
            if (state?.Bag == null || state.Bag.Length == 0)
            {
                if (state?.Items != null && state.Items.Any(i => string.Equals(i.Type, "Key", StringComparison.OrdinalIgnoreCase)))
                {
                    await TryPickupKeyAsync();
                    state = await GetStateAsync();
                    if (state?.Bag == null || state.Bag.Length == 0) return false;
                }
                else return false;
            }

            var dirInt = DirectionToInt(_dir);
            var prevX = _x;
            var prevY = _y;
            var bagCount = state?.Bag?.Length ?? 0;
            
            var unlockAndWalkPayload = new 
            { 
                id = _crawlerId, 
                x = _x, 
                y = _y, 
                direction = dirInt, 
                unlocking = true,
                walking = true
            };
            
            var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            req.Content = JsonContent.Create(unlockAndWalkPayload, options: _jsonOptions);
            await _http.SendAsync(req);
            
            var newState = await GetStateAsync();
            if (newState != null)
            {
                var newX = newState.GetX() ?? _x;
                var newY = newState.GetY() ?? _y;
                
                if (newX != prevX || newY != prevY)
                {
                    _x = newX;
                    _y = newY;
                    _dir = ParseDirection(newState.Direction, _dir);
                    return true;
                }
                
                var newBagCount = newState?.Bag?.Length ?? 0;
                if (newBagCount < bagCount) return true;
            }

            var unlockReq = new HttpRequestMessage(new HttpMethod("PATCH"), $"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            unlockReq.Content = JsonContent.Create(new { id = _crawlerId, x = _x, y = _y, direction = dirInt, unlocking = true }, options: _jsonOptions);
            var unlockRes = await _http.SendAsync(unlockReq);
            
            if (unlockRes.IsSuccessStatusCode)
            {
                newState = await GetStateAsync();
                var newBagCount = newState?.Bag?.Length ?? 0;
                if (newBagCount < bagCount) return true;
            }

            return false;
        }

        public async Task<Inventory?> TryWalkAsync(Inventory? keyChain)
        {
            var prevX = _x;
            var prevY = _y;

            var state = await GetStateAsync();
            if (state != null)
            {
                var sx = state.GetX();
                var sy = state.GetY();
                if (sx.HasValue) { prevX = _x; _x = sx.Value; }
                if (sy.HasValue) { prevY = _y; _y = sy.Value; }
                _dir = ParseDirection(state.Direction, _dir);
            }

            var dirInt = DirectionToInt(_dir);
            var payload = new { id = _crawlerId, x = _x, y = _y, direction = dirInt, walking = true };
            
            var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            req.Content = JsonContent.Create(payload, options: _jsonOptions);
            var res = await _http.SendAsync(req);

            if (res.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                state = await GetStateAsync();
                if (state != null)
                {
                    var sx = state.GetX();
                    var sy = state.GetY();
                    if (sx.HasValue) _x = sx.Value;
                    if (sy.HasValue) _y = sy.Value;
                    _dir = ParseDirection(state.Direction, _dir);

                    if (_x != prevX || _y != prevY)
                        return await TryPickupItemsOnTileAsync(state);
                }
                return null;
            }

            if (!res.IsSuccessStatusCode) return null;

            string? responseContent = null;
            try { responseContent = await res.Content.ReadAsStringAsync(); }
            catch { }

            CrawlerDto? dto = null;
            try
            {
                if (!string.IsNullOrEmpty(responseContent))
                    dto = JsonSerializer.Deserialize<CrawlerDto>(responseContent, _jsonOptions);
            }
            catch { return null; }

            if (dto == null) return new MyInventory();

            var nx = dto.GetX() ?? _x;
            var ny = dto.GetY() ?? _y;
            _x = nx; _y = ny; _dir = ParseDirection(dto.Direction, _dir);

            return await TryPickupItemsOnTileAsync(dto);
        }

        private async Task<Inventory> TryPickupItemsOnTileAsync(CrawlerDto? dto)
        {
            var inventory = new MyInventory();
            if (dto?.Items == null || dto.Items.Length == 0) return inventory;

            foreach (var item in dto.Items)
            {
                if (string.Equals(item.Type, "Key", StringComparison.OrdinalIgnoreCase))
                {
                    var picked = await TryPickupKeyAsync();
                    if (picked) inventory.AddItem(new Items.Key());
                }
            }

            return inventory;
        }

        private async Task<bool> TryPickupKeyAsync()
        {
            var state = await GetStateAsync();
            var currentBag = state?.Bag?.Length ?? 0;

            var req1 = new HttpRequestMessage(HttpMethod.Put, $"/crawlers/{_crawlerId}/items?appKey={Uri.EscapeDataString(_appKey)}");
            req1.Content = new StringContent("[{\"type\":\"Key\"}]", Encoding.UTF8, "application/json");
            var res1 = await _http.SendAsync(req1);
            
            if (res1.IsSuccessStatusCode)
            {
                var newState = await GetStateAsync();
                if ((newState?.Bag?.Length ?? 0) > currentBag) return true;
            }

            var req2 = new HttpRequestMessage(HttpMethod.Post, $"/crawlers/{_crawlerId}/items?appKey={Uri.EscapeDataString(_appKey)}");
            req2.Content = new StringContent("[{\"type\":\"Key\"}]", Encoding.UTF8, "application/json");
            var res2 = await _http.SendAsync(req2);
            
            if (res2.IsSuccessStatusCode)
            {
                var newState = await GetStateAsync();
                if ((newState?.Bag?.Length ?? 0) > currentBag) return true;
            }

            var req3 = new HttpRequestMessage(HttpMethod.Delete, $"/crawlers/{_crawlerId}/items?appKey={Uri.EscapeDataString(_appKey)}");
            await _http.SendAsync(req3);
            
            var stateAfter3 = await GetStateAsync();
            if ((stateAfter3?.Bag?.Length ?? 0) > currentBag) return true;

            var req4 = new HttpRequestMessage(new HttpMethod("PATCH"), $"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            req4.Content = JsonContent.Create(new 
            { 
                id = _crawlerId, 
                x = _x, 
                y = _y, 
                direction = DirectionToInt(_dir),
                bag = new[] { new { type = "Key" } }
            }, options: _jsonOptions);
            var res4 = await _http.SendAsync(req4);
            
            if (res4.IsSuccessStatusCode)
            {
                var newState = await GetStateAsync();
                if ((newState?.Bag?.Length ?? 0) > currentBag) return true;
            }

            var req5 = new HttpRequestMessage(HttpMethod.Put, $"/crawlers/{_crawlerId}/items?appKey={Uri.EscapeDataString(_appKey)}");
            req5.Content = new StringContent("[{\"type\":\"Key\",\"move-required\":true}]", Encoding.UTF8, "application/json");
            var res5 = await _http.SendAsync(req5);
            
            if (res5.IsSuccessStatusCode) return true;

            return false;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _http.DeleteAsync($"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            }
            catch { }
            finally
            {
                _http.Dispose();
            }
        }
    }
}
