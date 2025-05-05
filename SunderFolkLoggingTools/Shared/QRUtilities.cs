using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using QRCoder;

namespace SunderFolkLoggingTools.Shared;

/// <summary>
///     Provides utilities for working with QR codes and external IP address detection.
///     Handles retrieving the user's external IP address from various services,
///     and generating QR code images for connection URLs.
/// </summary>
internal static class QrUtilities
{
    /// <summary>
    ///     Cached external IP address to reduce API calls to IP detection services.
    /// </summary>
    private static string _cachedExternalIp;

    /// <summary>
    ///     Timestamp of the last IP address check to implement cache expiration.
    /// </summary>
    private static DateTime _lastIpCheck = DateTime.MinValue;

    /// <summary>
    ///     Retrieves the user's external IP address by querying multiple public IP detection services.
    ///     Results are cached for 5 minutes to reduce API calls and improve performance.
    /// </summary>
    /// <returns>The external IP address as a string, or null if detection failed</returns>
    public static string GetExternalIpAddress()
    {
        // Return cached IP if it's less than 5 minutes old
        if (_cachedExternalIp != null && (DateTime.Now - _lastIpCheck).TotalMinutes < 5)
            return _cachedExternalIp;

        try
        {
            using var client = new HttpClient();

            // List of IP detection services to try in sequence
            string[] ipServices =
            [
                "https://api.ipify.org",
                "https://checkip.amazonaws.com/",
                "https://icanhazip.com/",
                "https://wtfismyip.com/text"
            ];

            // Try each service until we get a valid IP
            foreach (var service in ipServices)
                try
                {
                    var ip = client.GetStringAsync(service).Result.Trim();

                    // Validate that the response is a properly formatted IPv4 address
                    if (Regex.IsMatch(ip, @"^\d{1,3}(\.\d{1,3}){3}$"))
                    {
                        // Cache the result and update the timestamp
                        _cachedExternalIp = ip;
                        _lastIpCheck = DateTime.Now;

                        if (PluginConfig.DevMode.Value)
                            Plugin.Log.LogInfo($"External IP from {service}: {ip}");

                        return ip;
                    }
                }
                catch
                {
                    // Silently continue to the next service if this one fails
                    // This allows graceful fallback between services
                }
        }
        catch (Exception ex)
        {
            if (PluginConfig.DevMode.Value)
                Plugin.Log.LogError($"IP detection failed: {ex.Message}");
        }

        // Return null if all detection methods failed
        return null;
    }

    /// <summary>
    ///     Generates a QR code image from a URI and opens it with the system's default image viewer.
    ///     Only runs if QR image generation is enabled in the plugin configuration.
    /// </summary>
    /// <param name="uri">The URI to encode in the QR code</param>
    public static void GenerateAndOpenQr(string uri)
    {
        // Exit early if QR image generation is disabled
        if (!PluginConfig.EnableQrImageGeneration.Value)
            return;

        try
        {
            // Create a QR code generator with medium error correction level
            var generator = new QRCodeGenerator();
            var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);

            // Convert the QR code to a bitmap
            var qrCode = new BitmapByteQRCode(data);
            var qrBitmap = qrCode.GetGraphic(20); // 20 pixels per module

            // Save the bitmap to a temporary file
            var filePath = Path.Combine(Path.GetTempPath(), "sunderfolk_qr.png");
            File.WriteAllBytes(filePath, qrBitmap);

            // Open the file with the system's default image viewer
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });

            if (PluginConfig.DevMode.Value)
                Plugin.Log.LogInfo("QR image generated and opened.");
        }
        catch (Exception ex)
        {
            if (PluginConfig.DevMode.Value)
                Plugin.Log.LogError($"QR image generation failed: {ex.Message}");
        }
    }
}