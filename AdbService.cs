using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace App_xddq
{
    public class AdbService
    {
        public async Task<string> GetDevicesAsync()
        {
            return await RunAdbCommandAsync("devices");
        }

        public async Task<string> RunAdbCommandAsync(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "adb",
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                string output = await process.StandardOutput.ReadToEndAsync();
                process.WaitForExit();
                return output;
            }
            catch (Exception ex)
            {
                return $"ADB??????: {ex.Message}";
            }
        }
    }
}
