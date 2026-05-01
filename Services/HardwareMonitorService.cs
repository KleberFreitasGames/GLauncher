using System.Diagnostics;
using System.Management;
using System.Timers;
using Microsoft.Win32;
using LibreHardwareMonitor.Hardware;

namespace GameLauncher.Services;

public class HardwareMetrics
{
    public float CpuUsage { get; init; }
    public float CpuTemp { get; init; }
    public float GpuUsage { get; init; }
    public float GpuTemp { get; init; }
    public float RamUsage { get; init; }
    public string CpuName { get; init; } = "";
    public string GpuName { get; init; } = "";
    public string RamTotal { get; init; } = "";
    public List<string> StorageDrives { get; init; } = [];
}

public sealed class HardwareMonitorService : IDisposable
{
    private readonly Computer _computer;
    private readonly System.Timers.Timer _timer;
    private bool _disposed;
    private readonly List<string> _storageDrives;
    private PerformanceCounter? _cpuCounter;

    public event Action<HardwareMetrics>? MetricsUpdated;

    public HardwareMonitorService(double intervalMs = 1500)
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true
        };

        try { _computer.Open(); }
        catch { }

        try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); _cpuCounter.NextValue(); }
        catch { _cpuCounter = null; }

        _storageDrives = QueryStorageDrives();

        _timer = new System.Timers.Timer(intervalMs);
        _timer.Elapsed += OnTimerElapsed;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private static IEnumerable<ISensor> GetAllSensors(IHardware hw)
    {
        foreach (var sensor in hw.Sensors)
            yield return sensor;
        foreach (var sub in hw.SubHardware)
            foreach (var sensor in sub.Sensors)
                yield return sensor;
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            float cpuUsage = 0, cpuTemp = 0;
            float gpuUsage = 0, gpuTemp = 0;
            float ramUsage = 0;
            string cpuName = "", gpuName = "";
            float ramUsed = 0, ramAvailable = 0;

            DebugLogger.Log("[HardwareMonitor] Iniciando coleta de métricas...");

            foreach (var hw in _computer.Hardware)
            {
                hw.Update();
                foreach (var sub in hw.SubHardware)
                    sub.Update();

                var allSensors = GetAllSensors(hw);

                switch (hw.HardwareType)
                {
                    case HardwareType.Cpu:
                        if (string.IsNullOrEmpty(cpuName)) cpuName = hw.Name;
                        DebugLogger.Log($"[HardwareMonitor] CPU: {hw.Name}");

                        foreach (var sensor in allSensors)
                        {
                            // Procura por sensores de temperatura reais
                            if (sensor.SensorType == SensorType.Temperature && sensor.Value.HasValue && sensor.Value > 0)
                            {
                                DebugLogger.Log($"[HardwareMonitor] ✓ Encontrado sensor Temperature: {sensor.Name} = {sensor.Value}°C");
                                cpuTemp = sensor.Value.Value;
                                break;
                            }
                        }

                        // Fallback para AMD Ryzen: usar "CPU Core Max" que contém a temperatura real
                        if (cpuTemp == 0)
                        {
                            foreach (var sensor in allSensors)
                            {
                                if (sensor.Name.Contains("Core Max") && sensor.Value.HasValue && sensor.Value > 0)
                                {
                                    DebugLogger.Log($"[HardwareMonitor] Fallback AMD: {sensor.Name} = {sensor.Value}°C");
                                    cpuTemp = sensor.Value.Value;
                                    break;
                                }
                            }
                        }

                        // Último fallback: qualquer Load > 20°C (para AMD)
                        if (cpuTemp == 0)
                        {
                            foreach (var sensor in allSensors)
                            {
                                if (sensor.SensorType == SensorType.Load && 
                                    (sensor.Name.Contains("Total") || sensor.Name.Contains("Package")) && 
                                    sensor.Value.HasValue && sensor.Value > 20 && sensor.Value < 150)
                                {
                                    DebugLogger.Log($"[HardwareMonitor] Fallback Load: {sensor.Name} = {sensor.Value}°C");
                                    cpuTemp = sensor.Value.Value;
                                    break;
                                }
                            }
                        }

                        DebugLogger.Log($"[HardwareMonitor] CPU Temp Final (LibreHardwareMonitor): {cpuTemp}°C");
                        break;

                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        if (string.IsNullOrEmpty(gpuName)) gpuName = hw.Name;
                        foreach (var sensor in allSensors)
                        {
                            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core"))
                                gpuUsage = sensor.Value ?? 0;
                            if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Core"))
                                gpuTemp = sensor.Value ?? 0;
                        }
                        break;

                    case HardwareType.Memory:
                        foreach (var sensor in allSensors)
                        {
                            if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Memory"))
                                ramUsage = sensor.Value ?? 0;
                            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Used"))
                                ramUsed = sensor.Value ?? 0;
                            if (sensor.SensorType == SensorType.Data && sensor.Name.Contains("Available"))
                                ramAvailable = sensor.Value ?? 0;
                        }
                        break;

                    case HardwareType.Motherboard:
                        if (cpuTemp == 0)
                        {
                            foreach (var sensor in allSensors)
                            {
                                if (sensor.SensorType == SensorType.Temperature &&
                                    (sensor.Name.Contains("CPU") || sensor.Name.Contains("Package")) &&
                                    sensor.Value > 0)
                                { cpuTemp = sensor.Value ?? 0; break; }
                            }
                        }
                        break;
                }
            }

            if (cpuUsage == 0)
            {
                try { cpuUsage = _cpuCounter?.NextValue() ?? 0; }
                catch { }
            }

            if (cpuTemp == 0)
            {
                DebugLogger.Log($"[HardwareMonitor] CPU Temp = 0, usando AdvancedTemperatureReader...");
                cpuTemp = AdvancedTemperatureReader.GetCpuTemperatureAdvanced();
            }

            DebugLogger.Log($"[HardwareMonitor] Resultado Final - CpuTemp={cpuTemp:F1}°C");

            var totalRam = ramUsed + ramAvailable;
            var ramText = totalRam > 0 ? $"{totalRam:F0} GB" : "";

            MetricsUpdated?.Invoke(new HardwareMetrics
            {
                CpuUsage = cpuUsage,
                CpuTemp = cpuTemp,
                GpuUsage = gpuUsage,
                GpuTemp = gpuTemp,
                RamUsage = ramUsage,
                CpuName = cpuName,
                GpuName = gpuName,
                RamTotal = ramText,
                StorageDrives = _storageDrives
            });
        }
        catch { }
    }

    private static List<string> QueryStorageDrives()
    {
        var drives = new List<string>();
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Model, Size, MediaType FROM Win32_DiskDrive");
            foreach (var obj in searcher.Get())
            {
                var model = obj["Model"]?.ToString()?.Trim();
                var sizeBytes = obj["Size"] as ulong? ?? 0;
                if (string.IsNullOrEmpty(model)) continue;

                var sizeGb = sizeBytes / (1024.0 * 1024 * 1024);
                var sizeText = sizeGb >= 1000 ? $"{sizeGb / 1024:F1} TB" : $"{sizeGb:F0} GB";
                drives.Add($"{model} ({sizeText})");
            }
        }
        catch { }
        return drives;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Dispose();
        try { _cpuCounter?.Dispose(); } catch { }
        try { _computer.Close(); } catch { }
    }
}
