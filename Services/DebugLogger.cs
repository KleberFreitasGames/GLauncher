using System;
using System.IO;
using System.Text;

namespace GameLauncher.Services;

/// <summary>
/// Logger que escreve em arquivo para debug
/// </summary>
public static class DebugLogger
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GameLauncher", "debug.log");

    static DebugLogger()
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
        catch { }
    }

    public static void Log(string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = $"[{timestamp}] {message}";

            File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            System.Diagnostics.Debug.WriteLine(line);
        }
        catch { }
    }

    public static void ClearLog()
    {
        try
        {
            if (File.Exists(LogPath))
                File.Delete(LogPath);
        }
        catch { }
    }
}
