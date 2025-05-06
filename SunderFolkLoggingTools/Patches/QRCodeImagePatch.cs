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
        // Store the value in a local variable so we can access it in the lambda
        var localValue = value;

        // Use logging operation without accessing the ref parameter directly
        var result = LoggingHelper.LogOperation("QRCodeImage.SetValue", () =>
        {
            // Log the intercepted QR code value
            LoggingHelper.Logger.Info($"QR input: {localValue}");

            // Skip processing if the value is empty
            if (string.IsNullOrWhiteSpace(localValue))
            {
                LoggingHelper.Logger.Warning("Empty URL, skipping patch");
                return (true, localValue); // Return tuple with result and unchanged value
            }

            try
            {
                // Parse the URL and extract query parameters
                var uri = new Uri(localValue);
                var query = HttpUtility.ParseQueryString(uri.Query);
                var joinParam = query.Get("join");

                // Skip if there's no join parameter (unexpected format)
                if (string.IsNullOrWhiteSpace(joinParam))
                {
                    LoggingHelper.Logger.Warning("No 'join' parameter in URL");
                    return (true, localValue); // Return tuple with result and unchanged value
                }

                // Process the join parameter
                LoggingHelper.Logger.BeginGroup("Join Parameter Analysis");
                var decoded = LoggingHelper.ProcessBase64("Join", joinParam, true);
                if (decoded != null)
                    LoggingHelper.LogMessagePackData(joinParam);
                LoggingHelper.Logger.EndGroup();

                // Get the external IP address for replacing the local one
                var externalIP = QrUtilities.GetExternalIpAddress();
                if (string.IsNullOrEmpty(externalIP))
                {
                    LoggingHelper.Logger.Warning("Failed to get external IP address");
                    return (true, localValue); // Return tuple with result and unchanged value
                }

                // Replace the IP in the MessagePack data and construct a new URL
                LoggingHelper.Logger.BeginGroup("IP Replacement");
                var modifiedBase64 = MessagePackUtilities.ReplaceIPInMessagePack(joinParam, externalIP);
                LoggingHelper.Logger.EndGroup();

                // Verify we got a valid result back
                if (string.IsNullOrEmpty(modifiedBase64))
                {
                    LoggingHelper.Logger.Warning("IP replacement failed");
                    return (true, localValue); // Return tuple with result and unchanged value
                }

                var patchedUrl = $"https://play.sunderfolk.com/?join={modifiedBase64}&p=2";
                LoggingHelper.Logger.Result("Modified URL", patchedUrl);

                return (true, patchedUrl); // Return tuple with result and modified value
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException("QRCodeImage.SetValue", ex);
                return (true, localValue); // Return tuple with result and unchanged value
            }
        });

        // Now assign the result back to the ref parameter
        if (result.Item2 != localValue) // Only assign if changed
            value = result.Item2;
        return result.Item1; // Return whether to continue with original method
    }
}