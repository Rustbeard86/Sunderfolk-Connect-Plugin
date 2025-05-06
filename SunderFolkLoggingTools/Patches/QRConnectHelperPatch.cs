using System;
using System.Text.RegularExpressions;
using Blackrazor.Runtime.Shared.Networking;
using HarmonyLib;
using SunderFolkLoggingTools.Shared;

// ReSharper disable InconsistentNaming

namespace SunderFolkLoggingTools.Patches;

/// <summary>
///     Harmony patch for QRConnectHelper.ToWebSafeBase64 method.
///     This patch intercepts the base64 encoding process for QR codes in SunderFolk,
///     replacing local IP addresses with external IPs to enable connections from
///     outside the local network. It can also generate QR images for quick scanning.
/// </summary>
[HarmonyPatch(typeof(QRConnectHelper), "ToWebSafeBase64")]
internal static class QRConnectHelperPatch
{
    /// <summary>
    ///     Harmony prefix method that intercepts and potentially modifies the
    ///     ToWebSafeBase64 method's behavior.
    /// </summary>
    /// <param name="val">The original Base64 string containing connection information</param>
    /// <param name="__result">Reference parameter to set the result of the method</param>
    /// <returns>True to continue with the original method execution, false to skip it</returns>
    public static bool Prefix(string val, ref string __result)
    {
        LoggingHelper.LogOperationBoundary("QRConnectHelper.ToWebSafeBase64", true);
        LoggingHelper.LogBase64Operation("Input Base64 String", val, true);

        try
        {
            // Normalize the Base64 string (web-safe to standard) and handle padding
            var normalizedBase64 = val.Replace('-', '+').Replace('_', '/');

            // Add padding if needed to make it a valid Base64 string
            var mod4 = normalizedBase64.Length % 4;
            if (mod4 > 0)
            {
                var padding = new string('=', 4 - mod4);
                normalizedBase64 += padding;

                if (PluginConfig.DevMode.Value)
                    Plugin.Log.LogInfo($"Added {4 - mod4} padding characters to Base64 string");
            }

            // Try to decode the Base64 string
            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(normalizedBase64);
                LoggingHelper.LogDataProcessing("Decoded Base64", decoded);
            }
            catch (Exception ex)
            {
                if (PluginConfig.DevMode.Value)
                {
                    Plugin.Log.LogError($"Failed to decode Base64 string: {ex.Message}");
                    // Try to identify problematic characters
                    var invalidChars = Regex.Replace(normalizedBase64, @"[A-Za-z0-9\+/=]", "");
                    if (invalidChars.Length > 0)
                        Plugin.Log.LogError($"Found invalid Base64 characters: '{invalidChars}'");
                }

                // Continue with the original method as fallback
                LoggingHelper.LogOperationBoundary("QRConnectHelper.ToWebSafeBase64", false);
                return true;
            }

            // In development mode, log the first few bytes for debugging
            if (decoded.Length > 0 && PluginConfig.DevMode.Value)
            {
                var hex = BitConverter.ToString(decoded, 0, Math.Min(32, decoded.Length)).Replace("-", " ");
                Plugin.Log.LogInfo($"First bytes (hex): {hex}...");
            }

            // Get the external IP address
            var externalIP = QrUtilities.GetExternalIpAddress();
            if (!string.IsNullOrEmpty(externalIP))
            {
                // Try to replace the local IP with the external IP
                var modifiedBase64 = MessagePackUtilities.ReplaceIPInMessagePack(val, externalIP);

                // If we successfully modified the Base64 string and got a valid result
                if (!string.IsNullOrEmpty(modifiedBase64) && modifiedBase64 != val)
                {
                    // Extract the port number from the decoded data
                    var port = MessagePackUtilities.ExtractPortFromMessagePack(decoded);

                    // Log the modified data in development mode
                    if (PluginConfig.DevMode.Value)
                    {
                        Plugin.Log.LogInfo($"Modified Base64 with external IP: {externalIP}");
                        Plugin.Log.LogInfo($"IP Address for connections {externalIP}:{port}");
                    }

                    // Create a connection URI with the modified data
                    var uri = $"https://play.sunderfolk.com/?join={modifiedBase64}&p=2";

                    if (PluginConfig.DevMode.Value)
                        Plugin.Log.LogInfo("============================================");

                    // Generate and open the QR code if enabled
                    QrUtilities.GenerateAndOpenQr(uri);
                    LoggingHelper.LogOperationBoundary("QRConnectHelper.ToWebSafeBase64", false);
                    return true;
                }
            }

            // If we couldn't modify the Base64 string, use the original value
            var originalUri = $"https://play.sunderfolk.com/?join={val}&p=2";

            if (PluginConfig.DevMode.Value)
            {
                Plugin.Log.LogInfo($"Generated URI for QR code (unmodified): {originalUri}");
                Plugin.Log.LogInfo("============================================");
            }

            // Generate and open the QR code with the original data
            QrUtilities.GenerateAndOpenQr(originalUri);
        }
        catch (Exception ex)
        {
            LoggingHelper.LogException("QRConnectHelper.ToWebSafeBase64", ex);

            // Try to generate a QR code with the original data as a fallback
            QrUtilities.GenerateAndOpenQr($"https://play.sunderfolk.com/?join={val}&p=2");
        }

        LoggingHelper.LogOperationBoundary("QRConnectHelper.ToWebSafeBase64", false);
        // Continue with the original method
        return true;
    }
}