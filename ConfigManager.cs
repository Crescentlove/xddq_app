using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace App_xddq
{
    public class ConfigManager : IConfigManager
    {
        private readonly string _configPath;
        private readonly Dictionary<string, Dictionary<string, (int x, int y)>> _map
            = new(StringComparer.OrdinalIgnoreCase);

        public ConfigManager()
        {
            _configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_configPath))
                {
                    CreateSampleConfig();
                }

                var json = File.ReadAllText(_configPath);
                if (string.IsNullOrWhiteSpace(json)) return;

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return;

                foreach (var sectionProp in root.EnumerateObject())
                {
                    var sectionName = sectionProp.Name;
                    if (!_map.TryGetValue(sectionName, out var keys))
                    {
                        keys = new Dictionary<string, (int x, int y)>(StringComparer.OrdinalIgnoreCase);
                        _map[sectionName] = keys;
                    }

                    var sectionValue = sectionProp.Value;
                    if (sectionValue.ValueKind != JsonValueKind.Object) continue;

                    foreach (var keyProp in sectionValue.EnumerateObject())
                    {
                        var keyName = keyProp.Name;
                        var pos = ParsePositionElement(keyProp.Value);
                        if (pos != null)
                            keys[keyName] = pos.Value;
                    }
                }
            }
            catch
            {
                // ignore parsing errors; leave map as-is
            }
        }

        private (int x, int y)? ParsePositionElement(JsonElement el)
        {
            try
            {
                switch (el.ValueKind)
                {
                    case JsonValueKind.Array:
                        {
                            var arr = el.EnumerateArray();
                            var vals = new List<int>();
                            foreach (var item in arr)
                            {
                                if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var n)) vals.Add(n);
                                else if (item.ValueKind == JsonValueKind.String && int.TryParse(item.GetString(), out var s)) vals.Add(s);
                                if (vals.Count >= 2) break;
                            }
                            if (vals.Count >= 2) return (vals[0], vals[1]);
                            return null;
                        }
                    case JsonValueKind.Object:
                        {
                            int? x = null, y = null;
                            if (el.TryGetProperty("x", out var px) && px.TryGetInt32(out var xi)) x = xi;
                            if (el.TryGetProperty("y", out var py) && py.TryGetInt32(out var yi)) y = yi;
                            if (x.HasValue && y.HasValue) return (x.Value, y.Value);

                            // try uppercase
                            if (el.TryGetProperty("X", out var pX) && pX.TryGetInt32(out var XI)) x = XI;
                            if (el.TryGetProperty("Y", out var pY) && pY.TryGetInt32(out var YI)) y = YI;
                            if (x.HasValue && y.HasValue) return (x.Value, y.Value);

                            return null;
                        }
                    case JsonValueKind.String:
                        {
                            var s = el.GetString();
                            if (string.IsNullOrWhiteSpace(s)) return null;
                            var parts = s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2 && int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var b))
                                return (a, b);
                            return null;
                        }
                    default:
                        return null;
                }
            }
            catch
            {
                return null;
            }
        }

        private void CreateSampleConfig()
        {
            try
            {
                var sample = new Dictionary<string, object>
                {
                    ["???"] = new Dictionary<string, int[]>
                    {
                        ["??????"] = new[] { 1160, 80 },
                        ["RETURN"] = new[] { 50, 50 }
                    },
                    ["???"] = new Dictionary<string, int[]>
                    {
                        ["??1"] = new[] { 600, 1400 },
                        ["??2"] = new[] { 800, 1400 },
                        ["??"] = new[] { 700, 1600 }
                    }
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(sample, options);
                File.WriteAllText(_configPath, json);
            }
            catch
            {
                // ignore write failures
            }
        }

        public (int x, int y)? GetPosition(string section, string key)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key)) return null;
            if (_map.TryGetValue(section, out var keys) && keys.TryGetValue(key, out var pos))
                return pos;
            return null;
        }

        // New methods for config editing
        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, (int x, int y)>> GetAll()
        {
            var result = new Dictionary<string, IReadOnlyDictionary<string, (int x, int y)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in _map)
            {
                result[kv.Key] = new Dictionary<string, (int x, int y)>(kv.Value, StringComparer.OrdinalIgnoreCase);
            }
            return result;
        }

        public void AddOrUpdatePosition(string section, string key, int x, int y)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key)) return;
            if (!_map.TryGetValue(section, out var keys))
            {
                keys = new Dictionary<string, (int x, int y)>(StringComparer.OrdinalIgnoreCase);
                _map[section] = keys;
            }
            keys[key] = (x, y);
        }

        public bool RemovePosition(string section, string key)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key)) return false;
            if (_map.TryGetValue(section, out var keys))
            {
                var removed = keys.Remove(key);
                if (keys.Count == 0)
                    _map.Remove(section);
                return removed;
            }
            return false;
        }

        public bool Save()
        {
            try
            {
                var obj = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var sec in _map)
                {
                    var inner = new Dictionary<string, int[]>();
                    foreach (var k in sec.Value)
                    {
                        inner[k.Key] = new[] { k.Value.x, k.Value.y };
                    }
                    obj[sec.Key] = inner;
                }
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(obj, options);
                File.WriteAllText(_configPath, json);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
