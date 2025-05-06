using System;
using System.Text.RegularExpressions;

namespace SunderFolkLoggingTools.Shared;

/// <summary>
///     Provides centralized logging functionality with configurable detail levels
///     based on development mode settings.
/// </summary>
internal static class LoggingHelper
{
    /// <summary>
    ///     Logs detailed information about data processing operations including raw data
    ///     in various formats to assist with troubleshooting.
    /// </summary>
    /// <param name="operation">Name of the operation being performed</param>
    /// <param name="data">Raw data being processed</param>
    /// <param name="additionalInfo">Additional contextual information</param>
    public static void LogDataProcessing(string operation, byte[] data, string additionalInfo = null)
    {
        if (!PluginConfig.DevMode.Value) return;

        Plugin.Log.LogInfo($"=== {operation} ===");

        if (data is { Length: > 0 })
        {
            var hexView = BitConverter.ToString(data, 0, Math.Min(64, data.Length)).Replace("-", " ");
            Plugin.Log.LogInfo($"Raw data (hex, first {Math.Min(64, data.Length)} bytes): {hexView}...");

            try
            {
                var base64View = Convert.ToBase64String(data, 0, Math.Min(64, data.Length));
                Plugin.Log.LogInfo($"Raw data (base64, first {Math.Min(64, data.Length)} bytes): {base64View}...");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed to convert data to Base64: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(additionalInfo))
            Plugin.Log.LogInfo($"Context: {additionalInfo}");
    }

    /// <summary>
    ///     Logs detailed information about Base64 string operations including
    ///     data validation to diagnose encoding/decoding issues.
    /// </summary>
    /// <param name="operation">Name of the operation being performed</param>
    /// <param name="base64String">The Base64 string being processed</param>
    /// <param name="isWebSafe">Whether the string is in web-safe Base64 format</param>
    public static void LogBase64Operation(string operation, string base64String, bool isWebSafe = false)
    {
        if (!PluginConfig.DevMode.Value) return;

        Plugin.Log.LogInfo($"=== {operation} ===");

        if (!string.IsNullOrEmpty(base64String))
        {
            // Log first part of the string to avoid console flooding
            var displayString = base64String.Length > 50
                ? base64String[..50] + "..."
                : base64String;

            Plugin.Log.LogInfo($"Base64 string ({base64String.Length} chars): {displayString}");

            // Validate if the string is proper Base64
            try
            {
                var normalizedBase64 = base64String;
                if (isWebSafe)
                    normalizedBase64 = base64String.Replace('-', '+').Replace('_', '/');

                // Adjust padding if needed
                var mod4 = normalizedBase64.Length % 4;
                if (mod4 > 0)
                {
                    Plugin.Log.LogWarning(
                        $"Base64 string length ({normalizedBase64.Length}) is not a multiple of 4. Missing padding?");
                    normalizedBase64 += new string('=', 4 - mod4);
                }

                // Try to decode to validate
                var decoded = Convert.FromBase64String(normalizedBase64);
                Plugin.Log.LogInfo($"Base64 string is valid. Decoded to {decoded.Length} bytes.");
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Invalid Base64 string: {ex.Message}");
                Plugin.Log.LogError("Base64 validation failed. String might be corrupted or incorrectly formatted.");

                // Try to pinpoint the issue
                try
                {
                    var invalidChars = Regex.Replace(
                        base64String,
                        isWebSafe ? @"[A-Za-z0-9\-_=]" : @"[A-Za-z0-9\+/=]",
                        "");

                    if (invalidChars.Length > 0)
                        Plugin.Log.LogError($"Found invalid Base64 characters: '{invalidChars}'");
                }
                catch
                {
                    // Ignore regex issues
                }
            }
        }
        else
        {
            Plugin.Log.LogWarning("Base64 string is null or empty");
        }
    }

    /// <summary>
    ///     Logs detailed exception information with consistent formatting.
    /// </summary>
    /// <param name="operation">Name of the operation that failed</param>
    /// <param name="ex">The exception that was thrown</param>
    public static void LogException(string operation, Exception ex)
    {
        if (!PluginConfig.DevMode.Value) return;

        Plugin.Log.LogError($"=== Error in {operation} ===");
        Plugin.Log.LogError($"Exception: {ex.GetType().FullName}");
        Plugin.Log.LogError($"Message: {ex.Message}");

        if (ex.StackTrace != null)
        {
            var stackLines = ex.StackTrace.Split('\n');
            Plugin.Log.LogError("Stack trace (first 5 lines):");
            for (var i = 0; i < Math.Min(5, stackLines.Length); i++) Plugin.Log.LogError($"  {stackLines[i].Trim()}");
        }

        if (ex.InnerException != null)
        {
            Plugin.Log.LogError($"Inner exception: {ex.InnerException.GetType().FullName}");
            Plugin.Log.LogError($"Inner message: {ex.InnerException.Message}");
        }
    }

    /// <summary>
    ///     Logs the beginning and end of major operations with consistent formatting.
    /// </summary>
    /// <param name="operationName">Name of the operation</param>
    /// <param name="isStart">True if this is the start of the operation, false if it's the end</param>
    public static void LogOperationBoundary(string operationName, bool isStart)
    {
        if (!PluginConfig.DevMode.Value) return;

        const string separator = "==================================================";
        if (isStart)
        {
            Plugin.Log.LogInfo(separator);
            Plugin.Log.LogInfo($"STARTING: {operationName} - {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
        }
        else
        {
            Plugin.Log.LogInfo($"COMPLETED: {operationName} - {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            Plugin.Log.LogInfo(separator);
        }
    }
}