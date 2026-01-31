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
using System.Diagnostics;

namespace Labyrinth.Crawl
{
    // HTTP-based ICrawler implementation that proxies crawler actions to a remote server API.
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

        // Helper: convert Direction to integer for API (0=North, 1=East, 2=South, 3=West)
        private static int DirectionToInt(Direction d) => d switch
        {
            var x when x.Equals(Direction.North) => 0,
            var x when x.Equals(Direction.East) => 1,
            var x when x.Equals(Direction.South) => 2,
            var x when x.Equals(Direction.West) => 3,
            _ => 0
        };

        // Helper: convert integer from API to Direction
        private static Direction IntToDirection(int i) => i switch
        {
            0 => Direction.North,
            1 => Direction.East,
            2 => Direction.South,
            3 => Direction.West,
            _ => Direction.North
        };

        // Helper: parse direction from string or int
        private static Direction ParseDirection(string? s, Direction fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            // Try parsing as int first
            if (int.TryParse(s, out var i)) return IntToDirection(i);
            // Otherwise parse as string name
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

        // Factory to create a remote crawler for the given appKey and baseUrl
        // optional ascii parameter: ignored, server always generates the labyrinth
        public static async Task<ApiCrawler> CreateAsync(string baseUrl, string appKey, string? ascii = null)
        {
            var http = new HttpClient { BaseAddress = new Uri(baseUrl) };

            // Attempt 1: send appKey as query parameter (legacy)
            var urlWithQuery = $"/crawlers?appKey={Uri.EscapeDataString(appKey)}";
            HttpResponseMessage res = null!;
            string? lastResponseContent = null;

            try
            {
                // Send null body
                res = await http.PostAsJsonAsync(urlWithQuery, (object?)null);

                if (!res.IsSuccessStatusCode)
                {
                    lastResponseContent = await ReadResponseContentSafeAsync(res);
                    // If forbidden or not success, try alternative auth header strategies
                    // Attempt 2: X-App-Key header
                    var request = new HttpRequestMessage(HttpMethod.Post, "/crawlers");
                    request.Content = JsonContent.Create((object?)null);
                    request.Headers.Add("X-App-Key", appKey);

                    res = await http.SendAsync(request);
                    if (!res.IsSuccessStatusCode)
                    {
                        lastResponseContent = await ReadResponseContentSafeAsync(res);
                        // Attempt 3: Authorization Bearer
                        var request2 = new HttpRequestMessage(HttpMethod.Post, "/crawlers");
                        request2.Content = JsonContent.Create((object?)null);
                        request2.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", appKey);

                        res = await http.SendAsync(request2);
                        if (!res.IsSuccessStatusCode)
                        {
                            lastResponseContent = await ReadResponseContentSafeAsync(res);
                            LogError("Create crawler failed", res, lastResponseContent);
                            throw new HttpRequestException($"Failed to create crawler. Status: {res.StatusCode}. Response: {lastResponseContent}");
                        }
                    }
                }
            }
            catch (HttpRequestException)
            {
                throw; // propagate
            }
            catch (Exception ex)
            {
                // Ensure we dispose http when we fail to construct the ApiCrawler
                http.Dispose();
                throw new InvalidOperationException("Error while creating remote crawler: " + ex.Message, ex);
            }

            // If we reach here, res is successful
            var json = await res.Content.ReadFromJsonAsync<JsonElement>();
            string id = json.GetProperty("id").GetString() ?? throw new InvalidOperationException("No crawler id returned");
            var client = new ApiCrawler(http, id, appKey);

            // try to read initial state
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
            catch (Exception ex)
            {
                LogError("Failed to read initial state", null, ex.Message);
            }

            return client;
        }

        private static async Task<string?> ReadResponseContentSafeAsync(HttpResponseMessage res)
        {
            try
            {
                return await res.Content.ReadAsStringAsync();
            }
            catch { return null; }
        }

        private static void LogError(string prefix, HttpResponseMessage? res, string? content)
        {
            var msg = new StringBuilder();
            msg.Append(prefix);
            if (res != null)
            {
                msg.Append($": Status={res.StatusCode}");
            }
            if (!string.IsNullOrEmpty(content))
            {
                msg.Append($" Response={content}");
            }
            var s = msg.ToString();
            try { Debug.WriteLine(s); } catch { }
            try { Console.Error.WriteLine(s); } catch { }
        }

        public int X => _x;
        public int Y => _y;

        public Direction Direction => _dir;

        // DTOs matching server schema (some server properties contain dashes)
        private class InventoryItemDto
        {
            [JsonPropertyName("type")] public string? Type { get; set; }

            [JsonPropertyName("move-required")] public bool? MoveRequired { get; set; }

            // alternate naming in some requests
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
            if (!res.IsSuccessStatusCode)
            {
                var content = await ReadResponseContentSafeAsync(res);
                LogError("GetState failed", res, content);
                return null;
            }

            try
            {
                var content = await res.Content.ReadAsStringAsync();
                Console.WriteLine($"[GetState] Full response: {content}");
                
                var dto = JsonSerializer.Deserialize<CrawlerDto>(content, _jsonOptions);
                return dto;
            }
            catch (Exception ex)
            {
                var content = await ReadResponseContentSafeAsync(res);
                LogError("Failed to deserialize state", null, ex.Message + "; content=" + content);
                return null;
            }
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
                catch
                {
                    return false;
                }
            }
        }

        public void TurnRight()
        {
            var newDirInt = _dir switch
            {
                var d when d.Equals(Direction.North) => 1, // East
                var d when d.Equals(Direction.East) => 2,  // South
                var d when d.Equals(Direction.South) => 3, // West
                _ => 0, // North
            };

            var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            // send full crawler object with current position and walking=false to avoid 409
            req.Content = JsonContent.Create(new { id = _crawlerId, x = _x, y = _y, direction = newDirInt, walking = false }, options: _jsonOptions);
            var res = _http.SendAsync(req).GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode)
            {
                var content = ReadResponseContentSafeAsync(res).GetAwaiter().GetResult();
                LogError("TurnRight failed", res, content);
                return;
            }

            _dir = IntToDirection(newDirInt);
        }

        public void TurnLeft()
        {
            var newDirInt = _dir switch
            {
                var d when d.Equals(Direction.North) => 3, // West
                var d when d.Equals(Direction.West) => 2,  // South
                var d when d.Equals(Direction.South) => 1, // East
                _ => 0, // North
            };

            var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            // send full crawler object with current position and walking=false to avoid 409
            req.Content = JsonContent.Create(new { id = _crawlerId, x = _x, y = _y, direction = newDirInt, walking = false }, options: _jsonOptions);
            var res = _http.SendAsync(req).GetAwaiter().GetResult();
            if (!res.IsSuccessStatusCode)
            {
                var content = ReadResponseContentSafeAsync(res).GetAwaiter().GetResult();
                LogError("TurnLeft failed", res, content);
                return;
            }

            _dir = IntToDirection(newDirInt);
        }

        public async Task<bool> TryUnlockAsync(Inventory keyChain)
        {
            // Build payload matching server schema
            var payload = new[] { new InventoryItemDto { Type = "Key", MoveRequired = true } };
            var json = JsonSerializer.Serialize(payload, _jsonOptions);
            var req = new HttpRequestMessage(HttpMethod.Put, $"/crawlers/{_crawlerId}/bag?appKey={Uri.EscapeDataString(_appKey)}");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var res = await _http.SendAsync(req);
            if (!res.IsSuccessStatusCode)
            {
                var content = await ReadResponseContentSafeAsync(res);
                LogError("TryUnlock failed", res, content);
                return false;
            }

            return true;
        }

        public async Task<Inventory?> TryWalkAsync(Inventory? keyChain)
        {
            // Save position before walking
            var prevX = _x;
            var prevY = _y;

            // Always refresh state before attempting to walk to avoid 409 conflicts
            var state = await GetStateAsync();
            if (state != null)
            {
                var sx = state.GetX();
                var sy = state.GetY();
                if (sx.HasValue) { prevX = _x; _x = sx.Value; }
                if (sy.HasValue) { prevY = _y; _y = sy.Value; }
                _dir = ParseDirection(state.Direction, _dir);
            }

            // Ask server to walk by sending a full crawler object with walking=true.
            // Use integer for direction
            var dirInt = DirectionToInt(_dir);
            var payload = new { id = _crawlerId, x = _x, y = _y, direction = dirInt, walking = true };
            
            // Log the request body
            var payloadJson = JsonSerializer.Serialize(payload, _jsonOptions);
            Console.WriteLine($"[TryWalk] PATCH /crawlers/{_crawlerId} Body: {payloadJson}");
            
            var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            req.Content = JsonContent.Create(payload, options: _jsonOptions);
            var res = await _http.SendAsync(req);

            // If server returned conflict, check if we moved anyway (another player may have opened door)
            if (res.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                var content = await ReadResponseContentSafeAsync(res);
                LogError("TryWalk conflict", res, content);

                // Refresh state to see if position actually changed
                state = await GetStateAsync();
                if (state != null)
                {
                    var sx = state.GetX();
                    var sy = state.GetY();
                    if (sx.HasValue) _x = sx.Value;
                    if (sy.HasValue) _y = sy.Value;
                    _dir = ParseDirection(state.Direction, _dir);

                    // If position changed, walk succeeded despite conflict message
                    if (_x != prevX || _y != prevY)
                    {
                        // Check if there are items in the new state and try to collect them
                        return await TryCollectItemsFromCurrentCellAsync();
                    }
                }

                return null; // Walk truly failed
            }

            if (!res.IsSuccessStatusCode)
            {
                var content = await ReadResponseContentSafeAsync(res);
                LogError("TryWalk failed", res, content);
                return null;
            }

            // Log the full response for debugging
            string? responseContent = null;
            try
            {
                responseContent = await res.Content.ReadAsStringAsync();
                Console.WriteLine($"[TryWalk] Response: {responseContent}");
            }
            catch { }

            CrawlerDto? dto = null;
            try
            {
                if (!string.IsNullOrEmpty(responseContent))
                {
                    dto = JsonSerializer.Deserialize<CrawlerDto>(responseContent, _jsonOptions);
                }
            }
            catch (Exception ex)
            {
                LogError("Failed to deserialize TryWalk response", null, ex.Message + "; content=" + responseContent);
                return null;
            }

            if (dto == null) return new MyInventory();

            var nx = dto.GetX() ?? _x;
            var ny = dto.GetY() ?? _y;

            _x = nx; _y = ny; _dir = ParseDirection(dto.Direction, _dir);

            // After successful walk, try to collect any items on the current cell
            return await TryCollectItemsFromCurrentCellAsync();
        }

        /// <summary>
        /// Extract inventory items from crawler state DTO.
        /// </summary>
        private static Inventory ExtractInventoryFromDto(CrawlerDto? dto)
        {
            if (dto == null) return new MyInventory();

            // Check "items" array (items on the tile we walked to)
            if (dto.Items != null && dto.Items.Length > 0)
            {
                var inv = new MyInventory();
                foreach (var it in dto.Items)
                {
                    if (string.Equals(it.Type, "Key", StringComparison.OrdinalIgnoreCase))
                    {
                        inv.AddItem(new Items.Key());
                        Console.WriteLine($"[TryWalk] Found key in 'items' array!");
                    }
                }
                if (inv.HasItems)
                {
                    Console.WriteLine($"[TryWalk] Collected {inv.Count} items from 'items' array");
                    return inv;
                }
            }

            // Check "bag" content (items already in crawler's bag)
            if (dto.Bag != null && dto.Bag.Length > 0)
            {
                var inv = new MyInventory();
                foreach (var it in dto.Bag)
                {
                    if (string.Equals(it.Type, "Key", StringComparison.OrdinalIgnoreCase))
                    {
                        inv.AddItem(new Items.Key());
                        Console.WriteLine($"[TryWalk] Found key in 'bag' array!");
                    }
                }
                if (inv.HasItems)
                {
                    Console.WriteLine($"[TryWalk] Collected {inv.Count} items from 'bag' array");
                    return inv;
                }
            }

            // Return empty inventory (walk succeeded but no items)
            return new MyInventory();
        }

        /// <summary>
        /// Extract inventory items from crawler state.
        /// </summary>
        private static Inventory ExtractInventoryFromState(CrawlerDto? state)
        {
            return ExtractInventoryFromDto(state);
        }

        /// <summary>
        /// Attempt to collect items from the current cell by sending a PUT request to pick up items.
        /// This checks for items on the tile and adds them to the crawler's bag.
        /// </summary>
        private async Task<Inventory> TryCollectItemsFromCurrentCellAsync()
        {
            var inventory = new MyInventory();

            // First, get the current state to see if there are items on the tile
            var state = await GetStateAsync();
            if (state == null) return inventory;

            // Check if there are items in the response
            if (state.Items != null && state.Items.Length > 0)
            {
                Console.WriteLine($"[Collect] Found {state.Items.Length} items on current tile!");
                
                // Try to pick up each item by adding it to our bag
                foreach (var item in state.Items)
                {
                    if (string.Equals(item.Type, "Key", StringComparison.OrdinalIgnoreCase))
                    {
                        // Send a PUT request to add the item to our bag
                        var pickupSuccess = await TryPickupItemAsync("Key");
                        if (pickupSuccess)
                        {
                            inventory.AddItem(new Items.Key());
                            Console.WriteLine($"[Collect] Successfully picked up a Key!");
                        }
                    }
                }
            }

            // Also check if we already have items in our bag from a previous collection
            if (state.Bag != null && state.Bag.Length > 0)
            {
                Console.WriteLine($"[Collect] Crawler bag contains {state.Bag.Length} items");
                foreach (var item in state.Bag)
                {
                    if (string.Equals(item.Type, "Key", StringComparison.OrdinalIgnoreCase))
                    {
                        inventory.AddItem(new Items.Key());
                        Console.WriteLine($"[Collect] Key already in bag!");
                    }
                }
            }

            return inventory;
        }

        /// <summary>
        /// Try to pick up an item from the current tile by sending a PUT to /crawlers/{id}/bag
        /// </summary>
        private async Task<bool> TryPickupItemAsync(string itemType)
        {
            try
            {
                // Send a PUT request to add the item to our bag with move-required = false (picking up)
                var payload = new[] { new { type = itemType, moveRequired = false } };
                var json = JsonSerializer.Serialize(payload, _jsonOptions);
                
                Console.WriteLine($"[Pickup] PUT /crawlers/{_crawlerId}/bag Body: {json}");
                
                var req = new HttpRequestMessage(HttpMethod.Put, $"/crawlers/{_crawlerId}/bag?appKey={Uri.EscapeDataString(_appKey)}");
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var res = await _http.SendAsync(req);

                var content = await ReadResponseContentSafeAsync(res);
                Console.WriteLine($"[Pickup] Response: {res.StatusCode} - {content}");

                return res.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Pickup] Error: {ex.Message}");
                return false;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await _http.DeleteAsync($"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            }
            catch { }
            _http.Dispose();
        }
    }
}
