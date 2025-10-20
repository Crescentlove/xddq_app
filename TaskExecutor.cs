using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace App_xddq
{
    public class TaskStep
    {
        public string Section { get; set; }
        public string Key { get; set; }
        public double Sleep { get; set; } // ?
    }

    public interface IConfigManager
    {
        // ????????(x, y)?null
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
        }

        private Dictionary<string, List<TaskStep>> LoadFuncStepsFromJson()
        {
            var dict = new Dictionary<string, List<TaskStep>>();
            string jsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "func_steps.json");
            if (!File.Exists(jsonPath)) return dict;
            var json = File.ReadAllText(jsonPath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var root = JsonSerializer.Deserialize<Dictionary<string, List<TaskStep>>>(json, options);
            if (root != null) dict = root;
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

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                AppendLog("???????");
            }
            catch { }
        }

        public async Task<string> RunFuncAsync(string funcName)
        {
            if (!_funcSteps.ContainsKey(funcName))
            {
                var msg = $"?????: {funcName}";
                AppendLog(msg);
                return msg;
            }

            if (IsRunning)
            {
                var busyMsg = $"????????????: {funcName}";
                AppendLog(busyMsg);
                return busyMsg;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsRunning = true;

            var steps = _funcSteps[funcName];
            string log = "";
            AppendLog($"????: {funcName}");
            try
            {
                foreach (var step in steps)
                {
                    if (token.IsCancellationRequested)
                    {
                        AppendLog($"???: {funcName}");
                        return "???";
                    }

                    var line = $"??: [{step.Section}] {step.Key}";
                    log += line + "\n";
                    AppendLog(line);
                    var pos = _configManager.GetPosition(step.Section, step.Key);
                    if (pos == null)
                    {
                        var miss = $"?????: {step.Section} - {step.Key}";
                        AppendLog(miss);
                        log += miss + "\n";
                        continue;
                    }

                    // ?? adb tap
                    await _adbService.RunAdbCommandAsync($"shell input tap {pos.Value.x} {pos.Value.y}");
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(step.Sleep), token);
                    }
                    catch (OperationCanceledException)
                    {
                        AppendLog($"?????: {funcName}");
                        return "???";
                    }
                }

                AppendLog($"??: {funcName}");
                return log + "??";
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
                var busy = "????????";
                AppendLog(busy);
                return busy;
            }

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            IsRunning = true;
            string log = "";
            AppendLog("????????");
            try
            {
                foreach (var name in funcNames)
                {
                    if (token.IsCancellationRequested)
                    {
                        AppendLog("???????");
                        log += "???\n";
                        break;
                    }
                    var res = await RunFuncAsync(name);
                    log += res + "\n";
                }
                AppendLog("??????");
                return log;
            }
            finally
            {
                IsRunning = false;
                try { _cts.Dispose(); } catch { }
                _cts = null;
            }
        }
    }
}
