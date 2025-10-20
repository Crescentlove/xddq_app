using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

        public async Task<string> RunFuncAsync(string funcName)
        {
            if (!_funcSteps.ContainsKey(funcName))
            {
                var msg = $"?????: {funcName}";
                AppendLog(msg);
                return msg;
            }
            var steps = _funcSteps[funcName];
            string log = "";
            AppendLog($"????: {funcName}");
            foreach (var step in steps)
            {
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
                await Task.Delay(TimeSpan.FromSeconds(step.Sleep));
            }
            AppendLog($"??: {funcName}");
            return log + "??";
        }

        public async Task<string> RunMultipleFuncsAsync(IEnumerable<string> funcNames)
        {
            string log = "";
            foreach (var name in funcNames)
            {
                var res = await RunFuncAsync(name);
                log += res + "\n";
            }
            return log;
        }
    }
}
