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

        public async Task<string> RunFuncAsync(string funcName)
        {
            if (!_funcSteps.ContainsKey(funcName))
                return $"???????: {funcName}";
            var steps = _funcSteps[funcName];
            string log = "";
            foreach (var step in steps)
            {
                log += $"??: [{step.Section}] {step.Key}\n";
                var pos = _configManager.GetPosition(step.Section, step.Key);
                if (pos == null)
                {
                    log += $"????????????: {step.Section} - {step.Key}\n";
                    continue;
                }
                // ????
                await _adbService.RunAdbCommandAsync($"shell input tap {pos.Value.x} {pos.Value.y}");
                await Task.Delay(TimeSpan.FromSeconds(step.Sleep));
            }
            return log + "??????";
        }

        public async Task<string> RunMultipleFuncsAsync(IEnumerable<string> funcNames)
        {
            string log = "";
            foreach (var name in funcNames)
            {
                log += await RunFuncAsync(name) + "\n";
            }
            return log;
        }
    }
}
