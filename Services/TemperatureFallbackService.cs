using System.Diagnostics;
using System.Management;

namespace GameLauncher.Services;

/// <summary>
/// Serviço alternativo para leitura de temperatura com múltiplas estratégias
/// </summary>
public sealed class TemperatureFallbackService
{
    /// <summary>
    /// Tenta ler temperatura da CPU com múltiplos métodos
    /// </summary>
    public static float GetCpuTemperature()
    {
        // Método 1: Via Process e WMI direto
        try
        {
            var temp = ReadCpuTempViaMsAcpi();
            if (temp > 0) return temp;
        }
        catch { }

        // Método 2: Via Performance Counter (rápido, mas requer setup)
        try
        {
            var temp = ReadCpuTempViaPerformanceCounter();
            if (temp > 0) return temp;
        }
        catch { }

        // Método 3: Comando PowerShell (último recurso)
        try
        {
            var temp = ReadCpuTempViaPowerShell();
            if (temp > 0) return temp;
        }
        catch { }

        return 0;
    }

    private static float ReadCpuTempViaMsAcpi()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", 
                "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature WHERE CurrentTemperature > 0");

            foreach (var obj in searcher.Get())
            {
                var kelvinTenths = Convert.ToSingle(obj["CurrentTemperature"]);
                var celsius = (kelvinTenths / 10f) - 273.15f;
                DebugLogger.Log($"[TempService] MSAcpi: {kelvinTenths} Kelvin/10 = {celsius}°C");

                if (celsius is > 20 and < 150)
                    return celsius;
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"[TempService] MSAcpi Error: {ex.Message}");
        }
        return 0;
    }

    private static float ReadCpuTempViaPerformanceCounter()
    {
        try
        {
            using var pc = new PerformanceCounter("Thermal Zone Information", "High Precision Temperature", @"\_TZ.THRM");
            var temp = pc.NextValue();
            DebugLogger.Log($"[TempService] PerformanceCounter: {temp}°C");

            if (temp is > 20 and < 150)
                return temp;
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"[TempService] PerformanceCounter Error: {ex.Message}");
        }
        return 0;
    }

    private static float ReadCpuTempViaPowerShell()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"Get-WmiObject MSAcpi_ThermalZoneTemperature -Namespace root/wmi | Select-Object -ExpandProperty CurrentTemperature\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) 
            {
                DebugLogger.Log($"[TempService] PowerShell: falhou ao iniciar processo");
                return 0;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000); // Timeout de 5 segundos

            if (string.IsNullOrEmpty(output)) 
            {
                DebugLogger.Log($"[TempService] PowerShell: sem output");
                return 0;
            }

            if (float.TryParse(output, out var kelvinTenths))
            {
                var celsius = (kelvinTenths / 10f) - 273.15f;
                DebugLogger.Log($"[TempService] PowerShell: {kelvinTenths} Kelvin/10 = {celsius}°C");
                if (celsius is > 20 and < 150)
                    return celsius;
            }
            else
            {
                DebugLogger.Log($"[TempService] PowerShell: falhou ao parsear '{output}'");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"[TempService] PowerShell Error: {ex.Message}");
        }
        return 0;
    }
}
