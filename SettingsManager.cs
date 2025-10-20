using System;
using System.IO;
using System.Text.Json;

namespace App_xddq
{
    public class SettingsManager
    {
        private readonly string _path;
        private SettingsData _data;

        private class SettingsData
        {
            public string ExportPath { get; set; }
            public string FuncStepsPath { get; set; }
            public string ConfigPath { get; set; }
        }

        public SettingsManager()
        {
            _path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            Load();
        }

        private void Load()
        {
            try
            {
                if (!File.Exists(_path))
                {
                    _data = new SettingsData();
                    Save();
                    return;
                }
                var json = File.ReadAllText(_path);
                _data = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
            }
            catch
            {
                _data = new SettingsData();
            }
        }

        public bool Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_path, json);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetExportPath()
        {
            if (!string.IsNullOrWhiteSpace(_data?.ExportPath)) return _data.ExportPath;
            // default to desktop
            return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        }

        public void SetExportPath(string path)
        {
            if (_data == null) _data = new SettingsData();
            _data.ExportPath = path;
            Save();
        }

        public string GetFuncStepsPath()
        {
            if (!string.IsNullOrWhiteSpace(_data?.FuncStepsPath)) return _data.FuncStepsPath;
            return null;
        }

        public void SetFuncStepsPath(string path)
        {
            if (_data == null) _data = new SettingsData();
            _data.FuncStepsPath = path;
            Save();
        }

        public string GetConfigPath()
        {
            if (!string.IsNullOrWhiteSpace(_data?.ConfigPath)) return _data.ConfigPath;
            return null;
        }

        public void SetConfigPath(string path)
        {
            if (_data == null) _data = new SettingsData();
            _data.ConfigPath = path;
            Save();
        }
    }
}
