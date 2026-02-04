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
                // Only log if there are items or bag content
                var dto = JsonSerializer.Deserialize<CrawlerDto>(content, _jsonOptions);
                if (dto?.Items?.Length > 0 || dto?.Bag?.Length > 0)
                {
                    Console.WriteLine($"[Keys] State has items:{dto?.Items?.Length ?? 0} bag:{dto?.Bag?.Length ?? 0}");
                }
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
            
            // Log the facing tile for debugging (only for non-Room tiles to reduce noise)
            if (facing != "Room" && facing != "Empty")
            {
                Console.WriteLine($"[Debug] Facing tile: '{facing}'");
            }
            
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
            // First, check if we have a key in the bag on the server
            var state = await GetStateAsync();
            if (state?.Bag == null || state.Bag.Length == 0)
            {
                // No keys in bag - try to pick up any keys on current tile first
                if (state?.Items != null && state.Items.Any(i => string.Equals(i.Type, "Key", StringComparison.OrdinalIgnoreCase)))
                {
                    await TryPickupKeyAsync();
                    state = await GetStateAsync();
                    if (state?.Bag == null || state.Bag.Length == 0)
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            var dirInt = DirectionToInt(_dir);
            var prevX = _x;
            var prevY = _y;
            var bagCount = state?.Bag?.Length ?? 0;
            
            Console.WriteLine($"[Keys] Attempting unlock+walk at ({_x},{_y}) dir={dirInt}, bag={bagCount}");
            
            // Strategy: PATCH with BOTH unlocking=true AND walking=true in the same call
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
            var res = await _http.SendAsync(req);
            
            // Check if we moved regardless of status code
            var newState = await GetStateAsync();
            if (newState != null)
            {
                var newX = newState.GetX() ?? _x;
                var newY = newState.GetY() ?? _y;
                
                if (newX != prevX || newY != prevY)
                {
                    Console.WriteLine($"[Keys] SUCCESS! Moved: ({prevX},{prevY}) -> ({newX},{newY})");
                    _x = newX;
                    _y = newY;
                    _dir = ParseDirection(newState.Direction, _dir);
                    return true;
                }
                
                // Check if key was consumed (door might be open now)
                var newBagCount = newState?.Bag?.Length ?? 0;
                if (newBagCount < bagCount)
                {
                    Console.WriteLine($"[Keys] Key consumed ({bagCount} -> {newBagCount}), door should be open");
                    // The door is now open, we need to walk through it
                    // The caller (MoveToAsync) will try TryForceStepForwardAsync again
                    return true;
                }
            }

            // If unlock+walk didn't work, try just unlocking then let caller walk
            var unlockReq = new HttpRequestMessage(new HttpMethod("PATCH"), $"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            unlockReq.Content = JsonContent.Create(new { id = _crawlerId, x = _x, y = _y, direction = dirInt, unlocking = true }, options: _jsonOptions);
            var unlockRes = await _http.SendAsync(unlockReq);
            
            if (unlockRes.IsSuccessStatusCode)
            {
                // Check if key was used
                newState = await GetStateAsync();
                var newBagCount = newState?.Bag?.Length ?? 0;
                if (newBagCount < bagCount)
                {
                    Console.WriteLine($"[Keys] Unlock consumed key ({bagCount} -> {newBagCount})");
                    return true;
                }
            }

            Console.WriteLine($"[Keys] Could not unlock door");
            return false;
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
            var dirInt = DirectionToInt(_dir);
            var payload = new { id = _crawlerId, x = _x, y = _y, direction = dirInt, walking = true };
            
            var req = new HttpRequestMessage(new HttpMethod("PATCH"), $"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
            req.Content = JsonContent.Create(payload, options: _jsonOptions);
            var res = await _http.SendAsync(req);

            // If server returned conflict, check if we moved anyway
            if (res.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                // Refresh state to see if position actually changed
                state = await GetStateAsync();
                if (state != null)
                {
                    var sx = state.GetX();
                    var sy = state.GetY();
                    if (sx.HasValue) _x = sx.Value;
                    if (sy.HasValue) _y = sy.Value;
                    _dir = ParseDirection(state.Direction, _dir);

                    if (_x != prevX || _y != prevY)
                    {
                        // Only return newly picked up items, not existing bag contents
                        return await TryPickupItemsOnTileAsync(state);
                    }
                }
                return null;
            }

            if (!res.IsSuccessStatusCode)
            {
                return null;
            }

            string? responseContent = null;
            try
            {
                responseContent = await res.Content.ReadAsStringAsync();
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
                LogError("Failed to deserialize TryWalk response", null, ex.Message);
                return null;
            }

            if (dto == null) return new MyInventory();

            var nx = dto.GetX() ?? _x;
            var ny = dto.GetY() ?? _y;

            _x = nx; _y = ny; _dir = ParseDirection(dto.Direction, _dir);

            // Only return newly picked up items from the tile, not existing bag contents
            return await TryPickupItemsOnTileAsync(dto);
        }

        /// <summary>
        /// Try to pick up items on the current tile (from "items" array in state).
        /// Returns an inventory with only the newly picked up items.
        /// </summary>
        private async Task<Inventory> TryPickupItemsOnTileAsync(CrawlerDto? dto)
        {
            var inventory = new MyInventory();
            
            if (dto?.Items == null || dto.Items.Length == 0)
            {
                return inventory;
            }

            Console.WriteLine($"[Keys] Found {dto.Items.Length} items on tile at ({_x},{_y})!");
            
            foreach (var item in dto.Items)
            {
                if (string.Equals(item.Type, "Key", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[Keys] KEY DETECTED! Attempting pickup...");
                    var picked = await TryPickupKeyAsync();
                    if (picked)
                    {
                        inventory.AddItem(new Items.Key());
                        Console.WriteLine($"[Keys] KEY PICKED UP SUCCESSFULLY!");
                    }
                    else
                    {
                        Console.WriteLine($"[Keys] Failed to pick up key");
                    }
                }
            }

            return inventory;
        }

        /// <summary>
        /// Try to pick up a key from the current tile.
        /// </summary>
        private async Task<bool> TryPickupKeyAsync()
        {
            // First refresh state to get current inventory version
            var state = await GetStateAsync();
            var currentItems = state?.Items?.Length ?? 0;
            var currentBag = state?.Bag?.Length ?? 0;
            
            // Try different pickup strategies
            
            // Strategy 1: PUT with just the type, no move-required
            var req1 = new HttpRequestMessage(HttpMethod.Put, $"/crawlers/{_crawlerId}/items?appKey={Uri.EscapeDataString(_appKey)}");
            req1.Content = new StringContent("[{\"type\":\"Key\"}]", Encoding.UTF8, "application/json");
            var res1 = await _http.SendAsync(req1);
            
            if (res1.IsSuccessStatusCode)
            {
                var newState = await GetStateAsync();
                if ((newState?.Bag?.Length ?? 0) > currentBag)
                {
                    Console.WriteLine($"[Keys] Pickup succeeded (strategy 1)!");
                    return true;
                }
            }

            // Strategy 2: POST to items endpoint
            var req2 = new HttpRequestMessage(HttpMethod.Post, $"/crawlers/{_crawlerId}/items?appKey={Uri.EscapeDataString(_appKey)}");
            req2.Content = new StringContent("[{\"type\":\"Key\"}]", Encoding.UTF8, "application/json");
            var res2 = await _http.SendAsync(req2);
            
            if (res2.IsSuccessStatusCode)
            {
                var newState = await GetStateAsync();
                if ((newState?.Bag?.Length ?? 0) > currentBag)
                {
                    Console.WriteLine($"[Keys] Pickup succeeded (strategy 2)!");
                    return true;
                }
            }

            // Strategy 3: DELETE from items (pick up = remove from tile)
            var req3 = new HttpRequestMessage(HttpMethod.Delete, $"/crawlers/{_crawlerId}/items?appKey={Uri.EscapeDataString(_appKey)}");
            var res3 = await _http.SendAsync(req3);
            
            if (res3.IsSuccessStatusCode)
            {
                var newState = await GetStateAsync();
                if ((newState?.Bag?.Length ?? 0) > currentBag)
                {
                    Console.WriteLine($"[Keys] Pickup succeeded (strategy 3)!");
                    return true;
                }
            }

            // Strategy 4: PATCH crawler with bag containing the key
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
                if ((newState?.Bag?.Length ?? 0) > currentBag)
                {
                    Console.WriteLine($"[Keys] Pickup succeeded (strategy 4)!");
                    return true;
                }
            }

            // Strategy 5: Original with move-required:true
            var req5 = new HttpRequestMessage(HttpMethod.Put, $"/crawlers/{_crawlerId}/items?appKey={Uri.EscapeDataString(_appKey)}");
            req5.Content = new StringContent("[{\"type\":\"Key\",\"move-required\":true}]", Encoding.UTF8, "application/json");
            var res5 = await _http.SendAsync(req5);
            var content5 = await ReadResponseContentSafeAsync(res5);
            
            Console.WriteLine($"[Keys] PUT /items: {res5.StatusCode} - {content5}");
            
            if (res5.IsSuccessStatusCode)
            {
                Console.WriteLine($"[Keys] Pickup succeeded (strategy 5)!");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extract inventory items from crawler state DTO.
        /// </summary>
        private static Inventory ExtractInventoryFromDto(CrawlerDto? dto)
        {
            if (dto == null) return new MyInventory();

            var inv = new MyInventory();

            if (dto.Items != null && dto.Items.Length > 0)
            {
                foreach (var it in dto.Items)
                {
                    if (string.Equals(it.Type, "Key", StringComparison.OrdinalIgnoreCase))
                    {
                        inv.AddItem(new Items.Key());
                        Console.WriteLine($"[Keys] Extracted key from items array");
                    }
                }
            }

            if (dto.Bag != null && dto.Bag.Length > 0)
            {
                foreach (var it in dto.Bag)
                {
                    if (string.Equals(it.Type, "Key", StringComparison.OrdinalIgnoreCase))
                    {
                        inv.AddItem(new Items.Key());
                        Console.WriteLine($"[Keys] Extracted key from bag array");
                    }
                }
            }

            return inv;
        }

        /// <summary>
        /// Extract inventory items from crawler state.
        /// </summary>
        private static Inventory ExtractInventoryFromState(CrawlerDto? state)
        {
            return ExtractInventoryFromDto(state);
        }

        /// <summary>
        /// Attempt to collect items from the current cell.
        /// </summary>
        private async Task<Inventory> TryCollectItemsFromCurrentCellAsync()
        {
            var state = await GetStateAsync();
            if (state == null) return new MyInventory();

            var inventory = new MyInventory();

            if (state.Items != null && state.Items.Length > 0)
            {
                Console.WriteLine($"[Keys] Collecting: {state.Items.Length} items on tile");
                
                foreach (var item in state.Items)
                {
                    if (string.Equals(item.Type, "Key", StringComparison.OrdinalIgnoreCase))
                    {
                        var pickupSuccess = await TryPickupKeyAsync();
                        if (pickupSuccess)
                        {
                            inventory.AddItem(new Items.Key());
                            Console.WriteLine($"[Keys] Key collected!");
                        }
                    }
                }
            }

            if (state.Bag != null && state.Bag.Length > 0)
            {
                foreach (var item in state.Bag)
                {
                    if (string.Equals(item.Type, "Key", StringComparison.OrdinalIgnoreCase))
                    {
                        inventory.AddItem(new Items.Key());
                        Console.WriteLine($"[Keys] Key in bag");
                    }
                }
            }

            return inventory;
        }

        /// <summary>
        /// Get items on the current tile by calling GET /crawlers/{id}/items
        /// </summary>
        private async Task<InventoryItemDto[]?> GetItemsOnCurrentTileAsync()
        {
            try
            {
                var res = await _http.GetAsync($"/crawlers/{_crawlerId}/items?appKey={Uri.EscapeDataString(_appKey)}");
                
                if (!res.IsSuccessStatusCode)
                {
                    return null;
                }

                var responseContent = await res.Content.ReadAsStringAsync();
                var items = JsonSerializer.Deserialize<InventoryItemDto[]>(responseContent, _jsonOptions);
                
                if (items != null && items.Length > 0)
                {
                    Console.WriteLine($"[Keys] GET /items returned {items.Length} items");
                }
                
                return items;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Try to pick up an item from the current tile
        /// </summary>
        private async Task<bool> TryPickupItemAsync(string itemType)
        {
            return await TryPickupKeyAsync();
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                Console.WriteLine($"[Cleanup] Deleting crawler {_crawlerId}...");
                var response = await _http.DeleteAsync($"/crawlers/{_crawlerId}?appKey={Uri.EscapeDataString(_appKey)}");
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Cleanup] Crawler {_crawlerId} deleted successfully.");
                }
                else
                {
                    var content = await ReadResponseContentSafeAsync(response);
                    Console.WriteLine($"[Cleanup] Failed to delete crawler {_crawlerId}: {response.StatusCode} - {content}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Cleanup] Error deleting crawler {_crawlerId}: {ex.Message}");
            }
            finally
            {
                _http.Dispose();
            }
        }
    }
}
