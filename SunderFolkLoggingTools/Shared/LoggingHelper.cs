using System;
using System.Collections.Generic;
using System.IO;
using SunderFolkLoggingTools.Tools;

namespace SunderFolkLoggingTools.Shared;

/// <summary>
///     Provides centralized logging functionality with configurable detail levels
///     based on development mode settings.
/// </summary>
internal static class LoggingHelper
{
    /// <summary>
    ///     Log level enumeration to categorize information by importance
    /// </summary>
    public enum LogLevel
    {
        Critical, // Always shown
        Info, // Basic info in DevMode
        Verbose, // Detailed info (shown if VerboseLogging enabled)
        Debug // Technical details (shown if DebugLogging enabled)
    }

    /// <summary>
    ///     Cached logging configuration to avoid checking config entries repeatedly
    /// </summary>
    private static LoggingConfig _config;

    /// <summary>
    ///     Get current logging configuration settings
    /// </summary>
    private static LoggingConfig Config
    {
        get
        {
            // Lazy init to avoid checking repeatedly
            if (_config.LastUpdated < DateTime.Now.AddSeconds(-10))
                _config = new LoggingConfig
                {
                    DevMode = PluginConfig.DevMode.Value,
                    VerboseLogging = PluginConfig.DevMode.Value,
                    DebugLogging = PluginConfig.DevMode.Value,
                    LastUpdated = DateTime.Now
                };
            return _config;
        }
    }

    /// <summary>
    ///     Logs an operation with automatic boundary management
    /// </summary>
    /// <param name="operation">Name of the operation</param>
    /// <param name="action">Action to perform and log</param>
    /// <returns>Result of the action</returns>
    public static T LogOperation<T>(string operation, Func<T> action)
    {
        try
        {
            Logger.BeginGroup($"{operation} - {DateTime.Now:HH:mm:ss.fff}");
            return action();
        }
        catch (Exception ex)
        {
            LogException(operation, ex);
            throw;
        }
        finally
        {
            Logger.EndGroup();
        }
    }

    /// <summary>
    ///     Logs an operation with automatic boundary management (void version)
    /// </summary>
    /// <param name="operation">Name of the operation</param>
    /// <param name="action">Action to perform and log</param>
    public static void LogOperation(string operation, Action action)
    {
        LogOperation<object>(operation, () =>
        {
            action();
            return null;
        });
    }

    /// <summary>
    ///     Process and log a Base64 string operation
    /// </summary>
    public static byte[] ProcessBase64(string operation, string base64String, bool isWebSafe = false)
    {
        if (string.IsNullOrEmpty(base64String))
        {
            Logger.Warning("Base64 string is null or empty");
            return null;
        }

        try
        {
            // Truncate for display
            var displayString = base64String.Length > 50
                ? base64String[..50] + "..."
                : base64String;

            Logger.Info($"Base64 string ({base64String.Length} chars): {displayString}");

            // Normalize and add padding
            var normalizedBase64 = base64String;
            if (isWebSafe)
                normalizedBase64 = base64String.Replace('-', '+').Replace('_', '/');

            // Adjust padding if needed
            var mod4 = normalizedBase64.Length % 4;
            if (mod4 > 0)
            {
                Logger.Verbose($"Adding {4 - mod4} padding characters");
                normalizedBase64 += new string('=', 4 - mod4);
            }

            // Decode
            var decoded = Convert.FromBase64String(normalizedBase64);
            Logger.Info($"Decoded to {decoded.Length} bytes");

            // Log debug data if enabled
            if (Config.DebugLogging) LogBinaryData(decoded);

            return decoded;
        }
        catch (Exception ex)
        {
            Logger.Error($"Base64 processing error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Log binary data details like hex representation
    /// </summary>
    public static void LogBinaryData(byte[] data)
    {
        if (data == null || data.Length == 0)
        {
            Logger.Warning("Binary data is null or empty");
            return;
        }

        var hexView = BitConverter.ToString(data, 0, Math.Min(64, data.Length)).Replace("-", " ");
        Logger.Debug(
            $"Binary data (first {Math.Min(64, data.Length)} bytes): {hexView}{(data.Length > 64 ? "..." : "")}");

        // Look for useful patterns
        var patterns = FindDataPatterns(data);
        foreach (var pattern in patterns) Logger.Info(pattern);
    }

    /// <summary>
    ///     Log MessagePack data in a concise way
    /// </summary>
    public static void LogMessagePackData(string base64Data, bool dumpToJson = true)
    {
        if (!Config.DevMode || string.IsNullOrEmpty(base64Data))
            return;

        try
        {
            var joinData = MessagePackDebugTool.DecodeFromBase64(base64Data);
            if (joinData?.ConnectionGroups == null || joinData.ConnectionGroups.Count == 0)
            {
                Logger.Warning("No connection data found");
                return;
            }

            // Show only key information
            var connectionCount = 0;
            foreach (var group in joinData.ConnectionGroups)
                connectionCount += group.Count;

            Logger.Info($"Found {connectionCount} connections in {joinData.ConnectionGroups.Count} groups");

            // Show first connection details
            var firstGroup = joinData.ConnectionGroups[0];
            if (firstGroup.Count > 0)
            {
                var connection = firstGroup[0];
                if (connection.Data.Length >= 4)
                {
                    var ip = $"{connection.Data[0]}.{connection.Data[1]}.{connection.Data[2]}.{connection.Data[3]}";
                    Logger.Info($"Primary connection: {ip}:{connection.Port}");
                }
            }

            // Dump to JSON file
            if (dumpToJson)
            {
                var path = Path.Combine(Path.GetTempPath(), $"sunderfolk_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                MessagePackDebugTool.DumpToJson(joinData, path);
                Logger.Verbose($"Saved detailed data to: {path}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to analyze MessagePack data: {ex.Message}");
        }
    }

    /// <summary>
    ///     Log IP replacement details
    /// </summary>
    public static void LogIpReplacement(string originalIp, string newIp, int port)
    {
        Logger.Info("Network Connection:");
        Logger.Info($"Local IP {originalIp} → External IP {newIp}");

        if (port > 0)
            Logger.Info($"Connection port: {port}");

        if (!string.IsNullOrEmpty(newIp) && port > 0)
            Logger.Info($"Full address: {newIp}:{port}");
    }

    /// <summary>
    ///     Logs detailed exception information with consistent formatting.
    /// </summary>
    public static void LogException(string operation, Exception ex)
    {
        Logger.Error($"Error in {operation}: {ex.GetType().Name}");
        Logger.Error(ex.Message);

        if (Config.VerboseLogging && ex.StackTrace != null)
        {
            var stackLines = ex.StackTrace.Split('\n');
            Logger.Error("Stack trace (first 3 lines):");
            for (var i = 0; i < Math.Min(3, stackLines.Length); i++)
                Logger.Error($"  {stackLines[i].Trim()}");

            if (ex.InnerException != null)
                Logger.Error($"Caused by: {ex.InnerException.Message}");
        }
    }

    /// <summary>
    ///     Simple console-like logger that groups and manages log output
    /// </summary>
    public static class Logger
    {
        /// <summary>
        ///     Currently active logging groups
        /// </summary>
        internal static readonly Stack<string> ActiveGroups = new();

        /// <summary>
        ///     Start a new logging group with a title
        /// </summary>
        public static void BeginGroup(string title, bool alwaysShow = false)
        {
            if (!ShouldLog(alwaysShow ? LogLevel.Critical : LogLevel.Info))
                return;

            ActiveGroups.Push(title);
            Log(LogLevel.Info, $"=== {title} ===");
        }

        /// <summary>
        ///     End the current logging group
        /// </summary>
        public static void EndGroup(bool showSeparator = true)
        {
            if (ActiveGroups.Count == 0) return;

            if (!ShouldLog(LogLevel.Info))
                return;

            var title = ActiveGroups.Pop();

            if (showSeparator && ActiveGroups.Count == 0)
                Log(LogLevel.Info, "==================================================");
        }

        /// <summary>
        ///     Write a log message with a specific log level
        /// </summary>
        public static void Write(LogLevel level, string message)
        {
            if (!ShouldLog(level))
                return;

            Log(level, message);
        }

        /// <summary>
        ///     Log an information message
        /// </summary>
        public static void Info(string message)
        {
            Write(LogLevel.Info, message);
        }

        /// <summary>
        ///     Log a verbose message (more details)
        /// </summary>
        public static void Verbose(string message)
        {
            Write(LogLevel.Verbose, message);
        }

        /// <summary>
        ///     Log a debug message (technical details)
        /// </summary>
        public static void Debug(string message)
        {
            Write(LogLevel.Debug, message);
        }

        /// <summary>
        ///     Log a warning message
        /// </summary>
        public static void Warning(string message)
        {
            Write(LogLevel.Critical, $"⚠️ {message}");
        }

        /// <summary>
        ///     Log an error message
        /// </summary>
        public static void Error(string message)
        {
            Write(LogLevel.Critical, $"❌ {message}");
        }

        /// <summary>
        ///     Log a result value with a label
        /// </summary>
        public static void Result(string label, object value)
        {
            if (!ShouldLog(LogLevel.Info))
                return;

            var valueStr = value?.ToString() ?? "null";
            if (valueStr.Length > 80)
                valueStr = valueStr[..80] + "...";

            Log(LogLevel.Info, $"{label}: {valueStr}");
        }
    }

    #region Private Helper Methods

    /// <summary>
    ///     Check if a message with this log level should be shown
    /// </summary>
    private static bool ShouldLog(LogLevel level)
    {
        return level switch
        {
            LogLevel.Critical => true,
            LogLevel.Info => Config.DevMode,
            LogLevel.Verbose => Config.DevMode && Config.VerboseLogging,
            LogLevel.Debug => Config.DevMode && Config.DebugLogging,
            _ => Config.DevMode
        };
    }

    /// <summary>
    ///     Forward log entries to the actual logging system
    /// </summary>
    private static void Log(LogLevel level, string message)
    {
        // Add indentation based on active groups
        var indent = "";
        if (level != LogLevel.Critical && Logger.ActiveGroups.Count > 1)
            indent = new string(' ', (Logger.ActiveGroups.Count - 1) * 2);

        switch (level)
        {
            case LogLevel.Critical:
                Plugin.Log.LogWarning(message);
                break;
            case LogLevel.Info:
                Plugin.Log.LogInfo(indent + message);
                break;
            case LogLevel.Verbose:
            case LogLevel.Debug:
                Plugin.Log.LogInfo(indent + (level == LogLevel.Debug ? "[DEBUG] " : "") + message);
                break;
        }
    }

    /// <summary>
    ///     Find interesting patterns in binary data
    /// </summary>
    private static List<string> FindDataPatterns(byte[] data)
    {
        var results = new List<string>();
        if (data == null || data.Length < 4)
            return results;

        // Find IP addresses
        for (var i = 0; i < data.Length - 4; i++)
            if ((data[i] == 192 && data[i + 1] == 168) || // 192.168.x.x
                data[i] == 10 || // 10.x.x.x
                (data[i] == 172 && data[i + 1] >= 16 && data[i + 1] <= 31)) // 172.16-31.x.x
            {
                results.Add($"IP at position {i}: {data[i]}.{data[i + 1]}.{data[i + 2]}.{data[i + 3]}");

                // Look for port after IP address
                if (i + 6 < data.Length)
                {
                    var port = (data[i + 4] << 8) | data[i + 5];
                    if (port > 1024 && port < 65535)
                        results.Add($"Port at position {i + 4}: {port}");
                }
            }

        return results;
    }

    /// <summary>
    ///     Configuration class for logging
    /// </summary>
    private struct LoggingConfig
    {
        public bool DevMode;
        public bool VerboseLogging;
        public bool DebugLogging;
        public DateTime LastUpdated;
    }

    #endregion
}