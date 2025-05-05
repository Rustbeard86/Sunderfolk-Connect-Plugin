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
                return true;

            // Get the external IP address for replacing the local one
            var externalIP = QrUtilities.GetExternalIpAddress();
            if (string.IsNullOrEmpty(externalIP))
                return true;

            // Replace the IP in the MessagePack data and construct a new URL
            var modifiedBase64 = MessagePackUtilities.ReplaceIPInMessagePack(joinParam, externalIP);
            var patchedUrl = $"https://play.sunderfolk.com/?join={modifiedBase64}&p=2";

            // Update the value and log the change
            Plugin.Log.LogInfo($"Overriding QR URL with patched IP: {patchedUrl}");
            value = patchedUrl;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"SetValue patch failed: {ex.Message}");
        }

        // Continue with the original method execution
        return true;
    }
}