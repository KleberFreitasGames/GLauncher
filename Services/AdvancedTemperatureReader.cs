using System;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace GameLauncher.Services;

/// <summary>
/// Estratégia avançada de leitura de temperatura suportando múltiplas fontes
/// </summary>
public sealed class AdvancedTemperatureReader
{
    public static float GetCpuTemperatureAdvanced()
    {
        // Estratégia 1: HWiNFO64 Registry
        var temp = ReadFromHWiNFO64Registry();
        if (temp > 0) { DebugLogger.Log($"[AdvancedTemp] ✓ HWiNFO64 Registry: {temp}°C"); return temp; }

        // Estratégia 2: OpenHardwareMonitor CLR
        temp = ReadFromOpenHardwareMonitor();
        if (temp > 0) { DebugLogger.Log($"[AdvancedTemp] ✓ OpenHardwareMonitor: {temp}°C"); return temp; }

        // Estratégia 3: Leitura via arquivo de log/cache de outros programas
        temp = ReadFromThirdPartyLogs();
        if (temp > 0) { DebugLogger.Log($"[AdvancedTemp] ✓ Third-party logs: {temp}°C"); return temp; }

        // Estratégia 4: CPU-Z
        temp = ReadFromCpuZ();
        if (temp > 0) { DebugLogger.Log($"[AdvancedTemp] ✓ CPU-Z: {temp}°C"); return temp; }

        DebugLogger.Log($"[AdvancedTemp] ✗ Nenhuma fonte conseguiu ler temperatura");
        return 0;
    }

    /// <summary>
    /// Tenta ler temperatura armazenada no Registry pelo HWiNFO64
    /// </summary>
    private static float ReadFromHWiNFO64Registry()
    {
        try
        {
            // HWiNFO64 salva valores em registro
            using var key = Registry.LocalMachine.OpenSubKey(@"Software\HWiNFO64\VSens");
            if (key == null) return 0;

            var values = key.GetValueNames();

            foreach (var valueName in values)
            {
                // Procura por valores que contenham "CPU" e temperatura
                if (valueName.Contains("CPU", StringComparison.OrdinalIgnoreCase) &&
                    (valueName.Contains("Temp", StringComparison.OrdinalIgnoreCase) || 
                     valueName.Contains("Die", StringComparison.OrdinalIgnoreCase) ||
                     valueName.Contains("Package", StringComparison.OrdinalIgnoreCase)))
                {
                    var obj = key.GetValue(valueName);
                    if (obj is double tempDouble && tempDouble > 20 && tempDouble < 150)
                    {
                        DebugLogger.Log($"[AdvancedTemp] HWiNFO64: {valueName} = {tempDouble}°C");
                        return (float)tempDouble;
                    }
                }
            }

            // Fallback: qualquer valor > 20°C que pareça temperatura
            foreach (var valueName in values)
            {
                var obj = key.GetValue(valueName);
                if (obj is double tempDouble && tempDouble > 35 && tempDouble < 150)
                {
                    DebugLogger.Log($"[AdvancedTemp] HWiNFO64 Fallback: {valueName} = {tempDouble}°C");
                    return (float)tempDouble;
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"[AdvancedTemp] HWiNFO64 Registry error: {ex.Message}");
        }
        return 0;
    }

    /// <summary>
    /// Tenta ler via OpenHardwareMonitor instalado localmente
    /// </summary>
    private static float ReadFromOpenHardwareMonitor()
    {
        try
        {
            // Se OpenHardwareMonitor estiver rodando, tenta WMI
            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation");

            var results = searcher.Get();
            foreach (var obj in results)
            {
                var temperature = obj["Temperature"] as ulong?;
                if (temperature.HasValue && temperature.Value > 0)
                {
                    var celsius = temperature.Value / 10f;
                    if (celsius is > 20 and < 150)
                    {
                        DebugLogger.Log($"[AdvancedTemp] OpenHardwareMonitor WMI: {celsius}°C");
                        return celsius;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"[AdvancedTemp] OpenHardwareMonitor error: {ex.Message}");
        }
        return 0;
    }

    /// <summary>
    /// Procura por arquivos de log/cache de programas de monitoramento
    /// </summary>
    private static float ReadFromThirdPartyLogs()
    {
        try
        {
            // Procura por arquivos comuns de log/cache
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "HWiNFO64", "HWiNFO64.log"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HWiNFO", "hwinfo.log"),
                @"C:\Program Files (x86)\35inchENG\temperature.dat",
                @"C:\Program Files (x86)\35inchENG\temp.dat",
                @"C:\Program Files (x86)\35inchENG\log.txt",
            };

            foreach (var path in possiblePaths)
            {
                if (!File.Exists(path)) continue;

                try
                {
                    var content = File.ReadAllText(path);
                    var temp = ExtractTemperatureFromText(content);
                    if (temp > 0)
                    {
                        DebugLogger.Log($"[AdvancedTemp] Found in {Path.GetFileName(path)}: {temp}°C");
                        return temp;
                    }
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"[AdvancedTemp] Third-party logs error: {ex.Message}");
        }
        return 0;
    }

    /// <summary>
    /// Tenta ler CPU-Z se estiver instalado
    /// </summary>
    private static float ReadFromCpuZ()
    {
        try
        {
            var cpuzPath = @"C:\Program Files (x86)\CPUID\CPU-Z\cpuz.exe";
            if (!File.Exists(cpuzPath)) return 0;

            var process = Process.Start(new ProcessStartInfo
            {
                FileName = cpuzPath,
                Arguments = "",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (process == null) return 0;

            // CPU-Z salva dados em arquivo, procura por ele
            var cpuzDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CPUID", "CPU-Z", "cpu-z.log");

            if (File.Exists(cpuzDataPath))
            {
                var content = File.ReadAllText(cpuzDataPath);
                var temp = ExtractTemperatureFromText(content);
                if (temp > 0) return temp;
            }

            process.WaitForExit(3000);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"[AdvancedTemp] CPU-Z error: {ex.Message}");
        }
        return 0;
    }

    /// <summary>
    /// Extrai temperatura de texto usando regex
    /// </summary>
    private static float ExtractTemperatureFromText(string text)
    {
        try
        {
            // Procura por padrões: "Temp: 45°C", "Temperature: 45", "CPU Temp: 45", etc
            var patterns = new[]
            {
                @"(?:CPU\s+)?Temp(?:erature)?[:\s]+(\d+(?:[.,]\d+)?)\s*°?C",
                @"(?:CPU|Processor|Core)\s+Temp(?:erature)?[:\s]+(\d+(?:[.,]\d+)?)",
                @"Tctl[:\s]+(\d+(?:[.,]\d+)?)",
                @"Tdie[:\s]+(\d+(?:[.,]\d+)?)",
                @"Package[:\s]+(\d+(?:[.,]\d+)?)\s*°?C",
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    var tempStr = match.Groups[1].Value.Replace(",", ".");
                    if (float.TryParse(tempStr, out var temp) && temp > 20 && temp < 150)
                    {
                        return temp;
                    }
                }
            }
        }
        catch { }
        return 0;
    }
}
