using System;
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
        // Log the input data when in development mode
        if (PluginConfig.DevMode.Value)
        {
            Plugin.Log.LogInfo("=== QRConnectHelper.ToWebSafeBase64 Input ===");
            Plugin.Log.LogInfo($"Base64 Input: {val}");
        }

        try
        {
            // Convert from web-safe Base64 to standard Base64, then decode to bytes
            var decoded = Convert.FromBase64String(val.Replace('-', '+').Replace('_', '/'));

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

                // If we successfully modified the Base64 string
                if (modifiedBase64 != val)
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
            // Log detailed error information in development mode
            if (PluginConfig.DevMode.Value)
            {
                Plugin.Log.LogError($"Failed to process QR: {ex}");
                Plugin.Log.LogError($"Type: {ex.GetType().FullName}");
            }

            // Try to generate a QR code with the original data as a fallback
            QrUtilities.GenerateAndOpenQr($"https://play.sunderfolk.com/?join={val}&p=2");
        }

        // Continue with the original method
        return true;
    }
}