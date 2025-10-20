using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;

namespace App_xddq
{
    public class ConfigManager : IConfigManager
    {
        private string _configPath;
        private readonly Dictionary<string, Dictionary<string, (int x, int y)>> _map
            = new(StringComparer.OrdinalIgnoreCase);

        public ConfigManager()
        {
            Load();
        }

        private static string EscapeForLog(string s)
        {
            if (s == null) return "<null>";
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s)
            {
                if (ch >= 32 && ch <= 126)
                {
                    sb.Append(ch);
                }
                else
                {
                    sb.AppendFormat("\\u{0:X4}", (int)ch);
                }
            }
            return sb.ToString();
        }

        private (string content, string encodingName) TryReadWithEncodings(string path)
        {
            var encodings = new List<System.Text.Encoding>
            {
                System.Text.Encoding.UTF8,
                System.Text.Encoding.Unicode, // UTF-16
                System.Text.Encoding.BigEndianUnicode,
            };
            try
            {
                encodings.Add(System.Text.Encoding.GetEncoding("GB18030"));
            }
            catch { }
            encodings.Add(System.Text.Encoding.Default);

            foreach (var enc in encodings)
            {
                try
                {
                    var text = File.ReadAllText(path, enc);
                    // quick check: can parse as JSON and has some non-ascii characters in property names
                    try
                    {
                        using var doc = JsonDocument.Parse(text);
                        var root = doc.RootElement;
                        if (root.ValueKind == JsonValueKind.Object)
                        {
                            bool hasNonAscii = false;
                            foreach (var prop in root.EnumerateObject())
                            {
                                if (prop.Name.Any(ch => ch > 127)) { hasNonAscii = true; break; }
                            }
                            if (hasNonAscii)
                                return (text, enc.WebName);
                            // if no non-ascii but valid JSON, keep this as fallback
                            return (text, enc.WebName);
                        }
                    }
                    catch { }
                }
                catch { }
            }
            return (null, null);
        }

        private void Load()
        {
            try
            {
                string defaultPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                string found = null;
                string content = null;
                string usedEncoding = null;

                // 1) If user configured a config path in settings, prefer it
                try
                {
                    var settings = new SettingsManager();
                    var configured = settings.GetConfigPath();
                    if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
                    {
                        var (txt, enc) = TryReadWithEncodings(configured);
                        if (!string.IsNullOrEmpty(txt))
                        {
                            content = txt; usedEncoding = enc; found = configured;
                            // write normalized UTF8 copy to app base dir
                            try
                            {
                                File.WriteAllText(defaultPath, content, System.Text.Encoding.UTF8);
                                AppendDebugLog($"Copied configured config.json from {configured} to {defaultPath}");
                                // set configPath to app copy
                                _configPath = defaultPath;
                            }
                            catch (Exception ex)
                            {
                                AppendDebugLog("Failed to copy configured config: " + ex.Message);
                                _configPath = configured;
                            }
                        }
                    }
                }
                catch { }

                // 2) If not set via settings, search candidate locations
                var candidates = new List<string>();
                var startDirs = new[] { AppDomain.CurrentDomain.BaseDirectory, AppContext.BaseDirectory, Environment.CurrentDirectory };
                foreach (var start in startDirs)
                {
                    if (string.IsNullOrWhiteSpace(start)) continue;
                    var current = start;
                    for (int i = 0; i < 8; i++)
                    {
                        var candidate = Path.Combine(current, "config.json");
                        if (File.Exists(candidate)) candidates.Add(candidate);
                        var parent = Directory.GetParent(current);
                        if (parent == null) break;
                        current = parent.FullName;
                    }
                }

                try
                {
                    var cur = AppDomain.CurrentDomain.BaseDirectory;
                    for (int i = 0; i < 12; i++)
                    {
                        if (string.IsNullOrEmpty(cur)) break;
                        try
                        {
                            if (Directory.EnumerateFiles(cur, "*.csproj").Any())
                            {
                                var candidate = Path.Combine(cur, "config.json");
                                if (File.Exists(candidate)) candidates.Add(candidate);
                            }
                        }
                        catch { }
                        var p = Directory.GetParent(cur);
                        if (p == null) break;
                        cur = p.FullName;
                    }
                }
                catch { }

                candidates = candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (string.IsNullOrEmpty(content))
                {
                    // pick first candidate and try read with encodings
                    string picked = null;
                    foreach (var c in candidates)
                    {
                        try
                        {
                            var (txt, enc) = TryReadWithEncodings(c);
                            if (!string.IsNullOrEmpty(txt))
                            {
                                // prefer file that contains non-ascii in property names
                                if (txt.Contains("???"))
                                {
                                    // likely garbled; skip if other candidates available
                                    if (picked == null) { picked = c; content = txt; usedEncoding = enc; }
                                    continue;
                                }
                                content = txt; usedEncoding = enc; picked = c; break;
                            }
                        }
                        catch { }
                    }
                    if (content == null && candidates.Count > 0)
                    {
                        picked = candidates[0];
                        try { content = File.ReadAllText(picked, System.Text.Encoding.UTF8); usedEncoding = System.Text.Encoding.UTF8.WebName; }
                        catch { }
                    }
                    if (picked != null) found = picked;
                }

                if (found == null)
                {
                    // not found, create sample at defaultPath
                    if (!File.Exists(defaultPath)) CreateSampleConfig();
                    found = defaultPath;
                }

                if (string.IsNullOrEmpty(content))
                {
                    try { content = File.ReadAllText(found, System.Text.Encoding.UTF8); usedEncoding = System.Text.Encoding.UTF8.WebName; }
                    catch { content = File.ReadAllText(found); usedEncoding = System.Text.Encoding.Default.WebName; }
                }

                // If we read content from elsewhere, ensure app copy exists
                try
                {
                    var dest = defaultPath;
                    if (!string.Equals(found, dest, StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.WriteAllText(dest, content, System.Text.Encoding.UTF8); AppendDebugLog($"Wrote normalized UTF8 config to {dest} (source encoding: {usedEncoding})"); } catch (Exception ex) { AppendDebugLog("Copy failed: " + ex.Message); }
                        _configPath = dest;
                    }
                    else
                    {
                        _configPath = found;
                    }
                }
                catch { _configPath = found; }

                // parse using content string
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return;

                foreach (var sectionProp in root.EnumerateObject())
                {
                    var sectionName = sectionProp.Name?.Trim();
                    if (string.IsNullOrEmpty(sectionName)) continue;
                    if (!_map.TryGetValue(sectionName, out var keys))
                    {
                        keys = new Dictionary<string, (int x, int y)>(StringComparer.OrdinalIgnoreCase);
                        _map[sectionName] = keys;
                    }

                    var sectionValue = sectionProp.Value;
                    if (sectionValue.ValueKind != JsonValueKind.Object) continue;

                    foreach (var keyProp in sectionValue.EnumerateObject())
                    {
                        var keyName = keyProp.Name?.Trim();
                        if (string.IsNullOrEmpty(keyName)) continue;
                        var pos = ParsePositionElement(keyProp.Value);
                        if (pos != null)
                            keys[keyName] = pos.Value;
                    }
                }

                // Detailed logging of loaded config for debugging
                try
                {
                    var settings = new SettingsManager();
                    var level = settings.GetLogLevel();
                    AppendDebugLog($"Loaded config.json from: {_configPath} (detected encoding: {usedEncoding})");
                    if (level == LogLevel.Debug)
                    {
                        foreach (var sec in _map)
                        {
                            AppendDebugLog($"Section: [{EscapeForLog(sec.Key)}] KeysCount: {sec.Value.Count}");
                            int i = 0;
                            foreach (var k in sec.Value.Keys)
                            {
                                i++; if (i > 20) { AppendDebugLog("  ... more keys ..."); break; }
                                AppendDebugLog($"  Key[{i}]: [{EscapeForLog(k)}]");
                            }
                        }
                    }
                }
                catch { }
            }
            catch
            {
                // ignore parsing errors; leave map as-is
            }
            finally
            {
                WriteConfigDebugDump(_configPath);
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
                                if (item.ValueKind == JsonValueKind.Number)
                                {
                                    if (item.TryGetInt32(out var n)) vals.Add(n);
                                    else
                                    {
                                        // try as double and convert
                                        try { var d = item.GetDouble(); vals.Add((int)Math.Round(d)); } catch { }
                                    }
                                }
                                else if (item.ValueKind == JsonValueKind.String)
                                {
                                    var s = item.GetString();
                                    if (int.TryParse(s, out var si)) vals.Add(si);
                                    else if (double.TryParse(s, out var sd)) vals.Add((int)Math.Round(sd));
                                }
                                if (vals.Count >= 2) break;
                            }
                            if (vals.Count >= 2) return (vals[0], vals[1]);
                            return null;
                        }
                    case JsonValueKind.Object:
                        {
                            double? x = null, y = null;
                            if (el.TryGetProperty("x", out var px))
                            {
                                if (px.ValueKind == JsonValueKind.Number)
                                {
                                    if (px.TryGetInt32(out var xi)) x = xi; else x = px.GetDouble();
                                }
                                else if (px.ValueKind == JsonValueKind.String && double.TryParse(px.GetString(), out var xd)) x = xd;
                            }
                            if (el.TryGetProperty("y", out var py))
                            {
                                if (py.ValueKind == JsonValueKind.Number)
                                {
                                    if (py.TryGetInt32(out var yi)) y = yi; else y = py.GetDouble();
                                }
                                else if (py.ValueKind == JsonValueKind.String && double.TryParse(py.GetString(), out var yd)) y = yd;
                            }
                            if (x.HasValue && y.HasValue) return ((int)Math.Round(x.Value), (int)Math.Round(y.Value));

                            // try uppercase
                            if (el.TryGetProperty("X", out var pX))
                            {
                                if (pX.ValueKind == JsonValueKind.Number)
                                {
                                    if (pX.TryGetInt32(out var XI)) x = XI; else x = pX.GetDouble();
                                }
                                else if (pX.ValueKind == JsonValueKind.String && double.TryParse(pX.GetString(), out var Xd)) x = Xd;
                            }
                            if (el.TryGetProperty("Y", out var pY))
                            {
                                if (pY.ValueKind == JsonValueKind.Number)
                                {
                                    if (pY.TryGetInt32(out var YI)) y = YI; else y = pY.GetDouble();
                                }
                                else if (pY.ValueKind == JsonValueKind.String && double.TryParse(pY.GetString(), out var Yd)) y = Yd;
                            }
                            if (x.HasValue && y.HasValue) return ((int)Math.Round(x.Value), (int)Math.Round(y.Value));

                            return null;
                        }
                    case JsonValueKind.String:
                        {
                            var s = el.GetString();
                            if (string.IsNullOrWhiteSpace(s)) return null;
                            var parts = s.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                if (int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var b)) return (a, b);
                                if (double.TryParse(parts[0], out var da) && double.TryParse(parts[1], out var db)) return ((int)Math.Round(da), (int)Math.Round(db));
                            }
                            return null;
                        }
                    case JsonValueKind.Number:
                        // unexpected single number
                        try
                        {
                            if (el.TryGetInt32(out var v)) return (v, v);
                            var dv = el.GetDouble();
                            var iv = (int)Math.Round(dv);
                            return (iv, iv);
                        }
                        catch { return null; }
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

        private static string NormalizeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            // trim and replace fullwidth space with normal space
            var t = s.Trim().Replace('\u3000', ' ');
            // remove zero-width characters
            t = t.Replace("\u200B", string.Empty).Replace("\uFEFF", string.Empty);
            return t;
        }

        public (int x, int y)? GetPosition(string section, string key)
        {
            if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key)) return null;

            // try exact
            if (_map.TryGetValue(section, out var keys) && keys.TryGetValue(key, out var pos))
                return pos;

            // try normalized exact
            var nSection = NormalizeName(section);
            var nKey = NormalizeName(key);
            if (_map.TryGetValue(nSection, out keys) && keys.TryGetValue(nKey, out pos))
                return pos;

            // try find a section by normalized equality or contains
            string matchedSection = null;
            foreach (var s in _map.Keys)
            {
                var ns = NormalizeName(s);
                if (string.Equals(ns, nSection, StringComparison.OrdinalIgnoreCase) || ns.IndexOf(nSection, StringComparison.OrdinalIgnoreCase) >= 0 || nSection.IndexOf(ns, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matchedSection = s;
                    break;
                }
            }

            if (matchedSection != null)
            {
                var sectionKeys = _map[matchedSection];
                // try exact or normalized key in that section
                if (sectionKeys.TryGetValue(key, out pos) || sectionKeys.TryGetValue(nKey, out pos))
                    return pos;

                // try fuzzy match for key
                foreach (var k in sectionKeys.Keys)
                {
                    var nk = NormalizeName(k);
                    if (string.Equals(nk, nKey, StringComparison.OrdinalIgnoreCase) || nk.IndexOf(nKey, StringComparison.OrdinalIgnoreCase) >= 0 || nKey.IndexOf(nk, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        // log mapping
                        try { AppendDebugLog($"Fuzzy match: requested Section='{EscapeForLog(section)}' Key='{EscapeForLog(key)}' => matched Section='{EscapeForLog(matchedSection)}' Key='{EscapeForLog(k)}'"); } catch { }
                        return sectionKeys[k];
                    }
                }
            }

            // as last resort, search all sections for a matching key
            foreach (var sec in _map)
            {
                foreach (var k in sec.Value.Keys)
                {
                    var nk = NormalizeName(k);
                    if (string.Equals(nk, nKey, StringComparison.OrdinalIgnoreCase) || nk.IndexOf(nKey, StringComparison.OrdinalIgnoreCase) >= 0 || nKey.IndexOf(nk, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try { AppendDebugLog($"Found key in different section: requested Section='{EscapeForLog(section)}' Key='{EscapeForLog(key)}' => matched Section='{EscapeForLog(sec.Key)}' Key='{EscapeForLog(k)}'"); } catch { }
                        return sec.Value[k];
                    }
                }
            }

            // Logging for debugging: write info to app.log
            try
            {
                string tmpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "tmp");
                if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir);
                string logPath = Path.Combine(tmpDir, "app.log");
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"GetPosition lookup failed for Section='{EscapeForLog(section)}' Key='{EscapeForLog(key)}'");
                if (_map.TryGetValue(section, out var existing))
                {
                    sb.AppendLine($"Section '{EscapeForLog(section)}' exists. Available keys: {string.Join(", ", existing.Keys)}");
                }
                else
                {
                    sb.AppendLine($"Section '{EscapeForLog(section)}' not found. Available sections: {string.Join(", ", _map.Keys)}");
                }
                File.AppendAllText(logPath, sb.ToString());
            }
            catch { }

            return null;
        }

        private void AppendDebugLog(string message)
        {
            try
            {
                string tmpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "tmp");
                if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir);
                string logPath = Path.Combine(tmpDir, "app.log");
                File.AppendAllText(logPath, message + Environment.NewLine, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        private static string ToCodePoints(string s)
        {
            if (s == null) return "<null>";
            var sb = new System.Text.StringBuilder();
            foreach (var ch in s)
            {
                sb.AppendFormat("U+{0:X4} ", (int)ch);
            }
            return sb.ToString().Trim();
        }

        private void WriteConfigDebugDump(string configPath)
        {
            try
            {
                string tmpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "tmp");
                if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir);
                var dbgPath = Path.Combine(tmpDir, "config_debug.txt");

                using var sw = new StreamWriter(dbgPath, false, System.Text.Encoding.UTF8);
                sw.WriteLine("Config debug dump");
                sw.WriteLine("Found path: " + configPath);

                // write BOM / first bytes
                var bytes = File.ReadAllBytes(configPath);
                sw.WriteLine("File length: " + bytes.Length);
                sw.WriteLine("First 64 bytes (hex):");
                for (int i = 0; i < Math.Min(64, bytes.Length); i++) sw.Write(bytes[i].ToString("X2") + " ");
                sw.WriteLine();

                // write first 400 chars
                var text = File.ReadAllText(configPath);
                sw.WriteLine("First 400 chars of file:");
                sw.WriteLine(text.Substring(0, Math.Min(400, text.Length)));
                sw.WriteLine();

                sw.WriteLine("Loaded sections and keys with codepoints:");
                foreach (var sec in _map)
                {
                    sw.WriteLine($"Section: [{sec.Key}] CodePoints: {ToCodePoints(sec.Key)}");
                    foreach (var k in sec.Value.Keys)
                    {
                        sw.WriteLine($"  Key: [{k}] CodePoints: {ToCodePoints(k)}");
                    }
                }

                sw.Flush();
            }
            catch { }
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

        public bool Reload(out string message)
        {
            message = null;
            try
            {
                _map.Clear();
                Load();
                message = "Reloaded config.";
                AppendDebugLog(message);
                return true;
            }
            catch (Exception ex)
            {
                message = "Reload failed: " + ex.Message;
                AppendDebugLog(message);
                return false;
            }
        }
    }
}
