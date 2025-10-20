using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace App_xddq
{
    public class TaskStep
    {
        public string Section { get; set; }
        public string Key { get; set; }
        public double Sleep { get; set; } // seconds to wait after action
    }

    public interface IConfigManager
    {
        // returns (x,y) or null if not found
        (int x, int y)? GetPosition(string section, string key);
    }

    public class TaskExecutor
    {
        private readonly AdbService _adbService;
        private readonly IConfigManager _configManager;
        private readonly Dictionary<string, List<TaskStep>> _funcSteps;

        // Real-time log event
        public event Action<string> LogUpdated;

        // Cancellation support
        private CancellationTokenSource _cts;
        public bool IsRunning { get; private set; }

        public TaskExecutor(AdbService adbService, IConfigManager configManager)
        {
            _adbService = adbService;
            _configManager = configManager;
            _funcSteps = LoadFuncStepsFromJson();

            // Log available functions to help debugging
            LogAvailableFunctions();
        }

        private Dictionary<string, List<TaskStep>> LoadFuncStepsFromJson()
        {
            var dict = new Dictionary<string, List<TaskStep>>(StringComparer.OrdinalIgnoreCase);
            string fileName = "func_steps.json";

            var tried = new List<string>();
            string foundPath = null;
            try
            {
                // first, check if SettingsManager provided a path
                try
                {
                    var settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
                    string configured = null;
                    if (File.Exists(settingsFile))
                    {
                        try
                        {
                            var sj = File.ReadAllText(settingsFile);
                            var doc = JsonDocument.Parse(sj);
                            if (doc.RootElement.TryGetProperty("FuncStepsPath", out var p) && p.ValueKind == JsonValueKind.String)
                                configured = p.GetString();
                        }
                        catch { }
                    }
                    if (!string.IsNullOrWhiteSpace(configured))
                    {
                        var cfgCandidate = configured;
                        if (!Path.IsPathRooted(cfgCandidate)) cfgCandidate = Path.GetFullPath(cfgCandidate);
                        if (File.Exists(cfgCandidate))
                        {
                            foundPath = cfgCandidate;
                        }
                    }
                }
                catch { }

                var startDirs = new[] { AppDomain.CurrentDomain.BaseDirectory, AppContext.BaseDirectory, Environment.CurrentDirectory };
                foreach (var start in startDirs)
                {
                    if (string.IsNullOrWhiteSpace(start)) continue;
                    var current = start;
                    for (int i = 0; i < 12; i++) // check up to 12 levels
                    {
                        var candidate = Path.Combine(current, fileName);
                        tried.Add(candidate);
                        if (File.Exists(candidate))
                        {
                            foundPath = candidate;
                            break;
                        }
                        var parent = Directory.GetParent(current);
                        if (parent == null) break;
                        current = parent.FullName;
                    }
                    if (foundPath != null) break;
                }

                // try to locate project root by searching for a .csproj upwards from AppDomain.BaseDirectory
                if (foundPath == null)
                {
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
                                    var candidate = Path.Combine(cur, fileName);
                                    tried.Add(candidate);
                                    if (File.Exists(candidate)) { foundPath = candidate; break; }
                                }
                            }
                            catch { }
                            var p = Directory.GetParent(cur);
                            if (p == null) break;
                            cur = p.FullName;
                        }
                    }
                    catch { }
                }

                if (foundPath == null)
                {
                    AppendLog($"func_steps.json not found. Tried: {string.Join("; ", tried.Take(20))}{(tried.Count > 20 ? "; ..." : string.Empty)}");
                    return dict;
                }

                AppendLog($"Loading func_steps from: {foundPath}");

                // copy file to app base dir to ensure future loads succeed
                try
                {
                    var dest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                    if (!File.Exists(dest))
                    {
                        File.Copy(foundPath, dest);
                        AppendLog($"Copied func_steps.json to: {dest}");
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("Could not copy func_steps.json: " + ex.Message);
                }

                var json = File.ReadAllText(foundPath);

                // Manual parsing to avoid reflection-based deserialization (source-gen / trimming issues)
                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var funcProp in root.EnumerateObject())
                        {
                            var funcName = (funcProp.Name ?? string.Empty).Trim();
                            if (string.IsNullOrEmpty(funcName)) continue;
                            var steps = new List<TaskStep>();
                            var arr = funcProp.Value;
                            if (arr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var item in arr.EnumerateArray())
                                {
                                    try
                                    {
                                        string section = null, key = null;
                                        double sleep = 0;
                                        if (item.ValueKind == JsonValueKind.Object)
                                        {
                                            if (item.TryGetProperty("section", out var ps) && ps.ValueKind == JsonValueKind.String) section = ps.GetString();
                                            if (item.TryGetProperty("key", out var pk) && pk.ValueKind == JsonValueKind.String) key = pk.GetString();
                                            if (item.TryGetProperty("sleep", out var psl))
                                            {
                                                if (psl.ValueKind == JsonValueKind.Number) sleep = psl.GetDouble();
                                                else if (psl.ValueKind == JsonValueKind.String && double.TryParse(psl.GetString(), out var d)) sleep = d;
                                            }
                                        }
                                        if (string.IsNullOrEmpty(section) || string.IsNullOrEmpty(key)) continue;
                                        steps.Add(new TaskStep { Section = section.Trim(), Key = key.Trim(), Sleep = sleep });
                                    }
                                    catch { }
                                }
                            }
                            if (steps.Count > 0)
                                dict[funcName] = steps;
                        }
                    }
                }
                catch (Exception ex)
                {
                    AppendLog("Error parsing func_steps.json: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                AppendLog("Error loading func_steps.json: " + ex.Message);
            }
            return dict;
        }

        private void AppendLog(string line)
        {
            try
            {
                string tmpDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "tmp");
                if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir);
                string logPath = Path.Combine(tmpDir, "app.log");
                File.AppendAllText(logPath, line + Environment.NewLine);
            }
            catch { }

            try
            {
                LogUpdated?.Invoke(line);
            }
            catch { }
        }

        private void LogAvailableFunctions()
        {
            try
            {
                if (_funcSteps == null || _funcSteps.Count == 0)
                {
                    AppendLog("No functions loaded from func_steps.json.");
                    return;
                }
                var names = string.Join(", ", _funcSteps.Keys);
                AppendLog("Loaded functions: " + names);
            }
            catch { }
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                AppendLog("Stop request sent");
            }
            catch { }
        }

        private bool TryGetSteps(string requested, out List<TaskStep> steps, out string matchedKey)
        {
            steps = null;
            matchedKey = null;
            if (string.IsNullOrWhiteSpace(requested)) return false;
            var name = requested.Trim();
            // direct lookup
            if (_funcSteps.TryGetValue(name, out steps))
            {
                matchedKey = name;
                return true;
            }
            // if ends with ??, try without
            const string suffix = "??";
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var without = name.Substring(0, name.Length - suffix.Length).Trim();
                if (_funcSteps.TryGetValue(without, out steps))
                {
                    matchedKey = without;
                    return true;
                }
            }
            else
            {
                // try with suffix
                var withSuffix = name + suffix;
                if (_funcSteps.TryGetValue(withSuffix, out steps))
                {
                    matchedKey = withSuffix;
                    return true;
                }
            }

            // also try full case-insensitive search for keys that contain the name
            foreach (var kv in _funcSteps)
            {
                if (kv.Key.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    steps = kv.Value;
                    matchedKey = kv.Key;
                    return true;
                }
            }

            return false;
        }

        public async Task<string> RunFuncAsync(string funcName)
        {
            if (string.IsNullOrWhiteSpace(funcName))
            {
                var msg = "Invalid function name.";
                AppendLog(msg);
                return msg;
            }

            // try to get steps using helper that attempts variants
            if (!TryGetSteps(funcName, out var steps, out var matchedKey) || steps == null || steps.Count == 0)
            {
                // provide helpful message listing available functions (limit length)
                var availList = string.Join(", ", _funcSteps.Keys);
                var avail = availList.Length > 800 ? availList.Substring(0, 800) + "..." : availList;
                var msg = $"Function not found: {funcName}. Available: {avail}";
                AppendLog(msg);
                return msg;
            }

            var effectiveName = matchedKey ?? funcName.Trim();

            if (IsRunning)
            {
                var busyMsg = $"Already running, cannot run: {effectiveName}";
                AppendLog(busyMsg);
                return busyMsg;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsRunning = true;

            string log = "";
            AppendLog($"Start executing: {effectiveName}");
            try
            {
                foreach (var step in steps)
                {
                    if (token.IsCancellationRequested)
                    {
                        AppendLog($"Canceled: {effectiveName}");
                        return "Canceled";
                    }

                    var line = $"Action: [{step.Section}] {step.Key}";
                    log += line + "\n";
                    AppendLog(line);
                    var pos = _configManager.GetPosition(step.Section, step.Key);
                    if (pos == null)
                    {
                        var miss = $"Unconfigured position: {step.Section} - {step.Key}";
                        AppendLog(miss);
                        log += miss + "\n";
                        continue;
                    }

                    // perform adb tap
                    await _adbService.RunAdbCommandAsync($"shell input tap {pos.Value.x} {pos.Value.y}");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(step.Sleep), token);
                    }
                    catch (OperationCanceledException)
                    {
                        AppendLog($"Delay canceled: {effectiveName}");
                        return "Canceled";
                    }
                }

                AppendLog($"Completed: {effectiveName}");
                var resultMsg = log + "Completed";
                // if user requested a name different from the matched one, include mapping info
                if (!string.Equals(funcName.Trim(), effectiveName, StringComparison.OrdinalIgnoreCase))
                {
                    resultMsg = $"Requested: {funcName.Trim()} => Matched: {effectiveName}\n" + resultMsg;
                }
                return resultMsg;
            }
            finally
            {
                IsRunning = false;
                try { _cts.Dispose(); } catch { }
                _cts = null;
            }
        }

        public async Task<string> RunMultipleFuncsAsync(IEnumerable<string> funcNames)
        {
            if (IsRunning)
            {
                var busy = "A task is already running.";
                AppendLog(busy);
                return busy;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsRunning = true;
            string log = "";
            AppendLog("Start batch execution");
            try
            {
                foreach (var name in funcNames)
                {
                    if (token.IsCancellationRequested)
                    {
                        AppendLog("Batch execution canceled");
                        log += "Canceled\n";
                        break;
                    }
                    var res = await RunFuncAsync(name);
                    log += res + "\n";
                }
                AppendLog("Batch execution finished");
                return log;
            }
            finally
            {
                IsRunning = false;
                try { _cts.Dispose(); } catch { }
                _cts = null;
            }
        }

        // Public API to copy a func_steps.json from a provided path into app base directory and reload
        public bool CopyFuncStepsFrom(string sourcePath, out string message)
        {
            message = null;
            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
                {
                    message = "Source file does not exist.";
                    AppendLog(message);
                    return false;
                }

                var dest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "func_steps.json");
                try
                {
                    File.Copy(sourcePath, dest, true);
                    AppendLog($"Copied func_steps.json to: {dest}");
                }
                catch (Exception ex)
                {
                    message = "Failed to copy file: " + ex.Message;
                    AppendLog(message);
                    return false;
                }

                // reload into memory
                try
                {
                    var newMap = LoadFuncStepsFromJson();
                    _funcSteps.Clear();
                    foreach (var kv in newMap)
                        _funcSteps[kv.Key] = kv.Value;

                    LogAvailableFunctions();
                    message = "Reloaded successfully.";
                    return true;
                }
                catch (Exception ex)
                {
                    message = "Copied but failed to reload: " + ex.Message;
                    AppendLog(message);
                    return false;
                }
            }
            catch (Exception ex)
            {
                message = "Unexpected error: " + ex.Message;
                AppendLog(message);
                return false;
            }
        }

        // Reload in-memory func steps from whatever func_steps.json is in base dir
        public bool ReloadFuncSteps(out string message)
        {
            message = null;
            try
            {
                var basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "func_steps.json");
                if (!File.Exists(basePath))
                {
                    message = "func_steps.json not found in application directory.";
                    AppendLog(message);
                    return false;
                }

                var newMap = LoadFuncStepsFromJson();
                _funcSteps.Clear();
                foreach (var kv in newMap)
                    _funcSteps[kv.Key] = kv.Value;

                AppendLog("Reloaded func_steps.json from application directory.");
                LogAvailableFunctions();
                message = "Reload successful.";
                return true;
            }
            catch (Exception ex)
            {
                message = "Reload failed: " + ex.Message;
                AppendLog(message);
                return false;
            }
        }
    }
}
