using System;
using System.Web;
using Blackrazor.Runtime.Shared.View.QRCode;
using HarmonyLib;
using SunderFolkLoggingTools.Shared;

// ReSharper disable InconsistentNaming

namespace SunderFolkLoggingTools.Patches;

/// <summary>
///     Harmony patch for the QRCodeImage.SetValue method.
///     Intercepts QR code generation to replace local IP addresses with external ones,
///     allowing connections from outside the local network.
/// </summary>
[HarmonyPatch(typeof(QRCodeImage), "SetValue")]
internal static class QRCodeImagePatch
{
    /// <summary>
    ///     Harmony prefix method that intercepts the SetValue call to modify QR code URLs.
    ///     Extracts connection parameters, replaces local IPs with external ones,
    ///     and updates the URL before it gets encoded as a QR code.
    /// </summary>
    /// <param name="__instance">The QRCodeImage instance being patched</param>
    /// <param name="value">The URL value that will be encoded in the QR code (modified by reference)</param>
    /// <returns>True to continue with the original method execution, false to skip it</returns>
    public static bool Prefix(QRCodeImage __instance, ref string value)
    {
        LoggingHelper.LogOperationBoundary("QRCodeImage.SetValue", true);

        // Log the intercepted QR code value when in development mode
        if (PluginConfig.DevMode.Value)
        {
            Plugin.Log.LogInfo("[QRCodeImage.SetValue] Intercepted QR input:");
            Plugin.Log.LogInfo($"Original value: {value}");
        }

        // Skip processing if the value is empty
        if (string.IsNullOrWhiteSpace(value))
        {
            if (PluginConfig.DevMode.Value)
                Plugin.Log.LogWarning("SetValue called with empty string. Skipping patch.");

            LoggingHelper.LogOperationBoundary("QRCodeImage.SetValue", false);
            return true;
        }

        try
        {
            // Parse the URL and extract query parameters
            var uri = new Uri(value);
            var query = HttpUtility.ParseQueryString(uri.Query);
            var joinParam = query.Get("join");

            // Skip if there's no join parameter (unexpected format)
            if (string.IsNullOrWhiteSpace(joinParam))
            {
                if (PluginConfig.DevMode.Value)
                    Plugin.Log.LogWarning("No 'join' parameter found in URL query string");

                LoggingHelper.LogOperationBoundary("QRCodeImage.SetValue", false);
                return true;
            }

            LoggingHelper.LogBase64Operation("Join parameter from URL", joinParam, true);

            // Get the external IP address for replacing the local one
            var externalIP = QrUtilities.GetExternalIpAddress();
            if (string.IsNullOrEmpty(externalIP))
            {
                if (PluginConfig.DevMode.Value)
                    Plugin.Log.LogWarning("Failed to get external IP address. Using original URL.");

                LoggingHelper.LogOperationBoundary("QRCodeImage.SetValue", false);
                return true;
            }

            // Replace the IP in the MessagePack data and construct a new URL
            var modifiedBase64 = MessagePackUtilities.ReplaceIPInMessagePack(joinParam, externalIP);

            // Verify we got a valid result back
            if (string.IsNullOrEmpty(modifiedBase64))
            {
                if (PluginConfig.DevMode.Value)
                    Plugin.Log.LogWarning("IP replacement returned empty string. Using original URL.");

                LoggingHelper.LogOperationBoundary("QRCodeImage.SetValue", false);
                return true;
            }

            var patchedUrl = $"https://play.sunderfolk.com/?join={modifiedBase64}&p=2";

            // Update the value and log the change
            if (PluginConfig.DevMode.Value)
                Plugin.Log.LogInfo($"Overriding QR URL with patched IP: {patchedUrl}");

            value = patchedUrl;
        }
        catch (Exception ex)
        {
            LoggingHelper.LogException("QRCodeImage.SetValue", ex);
        }

        LoggingHelper.LogOperationBoundary("QRCodeImage.SetValue", false);
        // Continue with the original method execution
        return true;
    }
}