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
        // We'll use a tuple to return both whether to continue original method and the possibly modified result
        var opResult = LoggingHelper.LogOperation("QRConnectHelper.ToWebSafeBase64", () =>
        {
            // Log the original input for debugging purposes
            LoggingHelper.Logger.Info("Processing connection data");

            // Process the base64 data for debugging
            if (PluginConfig.DevMode.Value) LoggingHelper.ProcessBase64("Input Base64 String", val);

            try
            {
                // Get the external IP address for replacing the local one
                var externalIP = QrUtilities.GetExternalIpAddress();
                if (string.IsNullOrEmpty(externalIP))
                {
                    LoggingHelper.Logger.Warning("Failed to get external IP address");
                    return (true, null); // Return tuple: (continueOriginal, result)
                }

                // Replace the IP in the MessagePack data
                var modifiedBase64 = MessagePackUtilities.ReplaceIPInMessagePack(val, externalIP);

                // Set the result to our modified value if successful
                if (!string.IsNullOrEmpty(modifiedBase64))
                {
                    LoggingHelper.Logger.Info($"Modified Base64 with external IP: {externalIP}");

                    // Try to determine the port to include it in the log
                    try
                    {
                        var decoded = Convert.FromBase64String(
                            modifiedBase64.Replace('-', '+').Replace('_', '/') +
                            new string('=', (4 - modifiedBase64.Length % 4) % 4)
                        );

                        var port = MessagePackUtilities.ExtractPortFromMessagePack(decoded);
                        if (port > 0) LoggingHelper.Logger.Info($"IP Address for connections {externalIP}:{port}");
                    }
                    catch
                    {
                        // Ignore errors in port detection
                    }

                    // Optionally generate a QR code image for the connection
                    if (PluginConfig.EnableQrImageGeneration.Value)
                        QrUtilities.GenerateAndOpenQr($"https://play.sunderfolk.com/?join={modifiedBase64}&p=2");

                    return (false, modifiedBase64); // Return tuple: (continueOriginal, result)
                }
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException("QRConnectHelper.ToWebSafeBase64", ex);
            }

            // If we get here, continue with the original method
            return (true, (string)null);
        });

        // Now handle the result tuple outside the lambda
        if (opResult.Item1 == false && opResult.Item2 != null) __result = opResult.Item2; // Set the ref parameter
        return opResult.Item1; // Return continuation flag
    }
}