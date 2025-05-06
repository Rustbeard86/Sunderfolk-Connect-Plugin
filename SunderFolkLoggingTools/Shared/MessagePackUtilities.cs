using System;
using SunderFolkLoggingTools.Bridges;

namespace SunderFolkLoggingTools.Shared;

/// <summary>
///     Provides utilities for manipulating MessagePack-encoded data in SunderFolk connection strings.
///     Specifically handles finding and replacing IP addresses and extracting port numbers
///     from binary MessagePack data.
/// </summary>
internal static class MessagePackUtilities
{
    /// <summary>
    ///     Finds and replaces a local IP address in MessagePack-encoded data with an external IP.
    ///     Supports common private IP address patterns (192.168.x.x, 10.x.x.x, and 172.16-31.x.x).
    /// </summary>
    /// <param name="base64String">The Base64-encoded MessagePack data containing network information</param>
    /// <param name="newIP">The new IP address to replace the local one with</param>
    /// <returns>Modified Base64 string with the replaced IP address, or the original string if no replacement was made</returns>
    internal static string ReplaceIPInMessagePack(string base64String, string newIP)
    {
        return LoggingHelper.LogOperation("ReplaceIPInMessagePack", () =>
        {
            try
            {
                // Use the bridge which uses MessagePackDebugTool
                var result = MessagePackBridge.ProcessJoinParameter(base64String, newIP);

                if (!result.Success)
                {
                    LoggingHelper.Logger.Warning($"MessagePack processing failed: {result.ErrorMessage}");
                    return base64String;
                }

                if (result.DidReplaceIP)
                    LoggingHelper.Logger.Info($"Replaced IP address with: {newIP}");

                return result.ModifiedBase64;
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException("ReplaceIPInMessagePack", ex);
                return base64String;
            }
        });
    }

    /// <summary>
    ///     Extracts a likely port number from MessagePack-encoded data by looking for
    ///     common port patterns after IP addresses or MessagePack integer markers.
    /// </summary>
    /// <param name="data">The raw MessagePack byte array</param>
    /// <returns>The extracted port number, or -1 if no valid port was found</returns>
    internal static int ExtractPortFromMessagePack(byte[] data)
    {
        try
        {
            // First strategy: search for port numbers after IP addresses (common in network protocols)
            var port = FindPortAfterIpAddress(data);
            if (port > 0)
                return port;

            // Second strategy: look for MessagePack integer markers followed by port values
            port = FindPortByMessagePackMarkers(data);
            if (port > 0)
                return port;

            // Last resort: brute force search for byte sequences in common port ranges
            port = BruteForcePortSearch(data);
            if (port > 0)
                return port;

            return -1;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    /// <summary>
    ///     Helper method to replace IP address bytes at the specified position.
    /// </summary>
    private static bool ReplaceIPBytes(byte[] data, int position, string newIP)
    {
        // Parse the new IP address
        var ipParts = newIP.Split('.');
        if (ipParts.Length == 4 &&
            byte.TryParse(ipParts[0], out var octet1) &&
            byte.TryParse(ipParts[1], out var octet2) &&
            byte.TryParse(ipParts[2], out var octet3) &&
            byte.TryParse(ipParts[3], out var octet4))
        {
            // Replace the IP bytes
            data[position] = octet1;
            data[position + 1] = octet2;
            data[position + 2] = octet3;
            data[position + 3] = octet4;

            LoggingHelper.Logger.Info($"Replaced IP with: {newIP}");
            return true;
        }

        return false;
    }

    /// <summary>
    ///     Converts a byte array to a web-safe Base64 string.
    /// </summary>
    private static string ConvertToWebSafeBase64(byte[] data)
    {
        try
        {
            // Convert to standard Base64 first
            var standardBase64 = Convert.ToBase64String(data);

            // Then convert to web-safe Base64 and remove any padding
            var webSafeBase64 = standardBase64.Replace('+', '-').Replace('/', '_').TrimEnd('=');

            LoggingHelper.Logger.Verbose($"Standard Base64: {standardBase64}");
            LoggingHelper.Logger.Verbose($"Web-safe Base64: {webSafeBase64}");

            return webSafeBase64;
        }
        catch (Exception ex)
        {
            LoggingHelper.Logger.Error($"Failed to convert to web-safe Base64: {ex.Message}");

            // Return empty string on failure to prevent further processing
            return string.Empty;
        }
    }

    /// <summary>
    ///     Searches for port numbers immediately following IP address patterns.
    /// </summary>
    private static int FindPortAfterIpAddress(byte[] data)
    {
        for (var i = 0; i < data.Length - 6; i++)
            // First check if we found an IP address
            if ((data[i] == 0xC0 && data[i + 1] == 0xA8) || // 192.168.x.x
                data[i] == 0x0A || // 10.x.x.x
                (data[i] == 0xAC && data[i + 1] >= 0x10 && data[i + 1] <= 0x1F)) // 172.16-31.x.x
                // After finding an IP address, look at nearby bytes for a port number
                // Check a few bytes after the IP (offset by 4-8 bytes is common)
                for (var offset = 4; offset < 16 && i + offset + 1 < data.Length; offset++)
                {
                    // Try big-endian encoding (most common)
                    var portBigEndian = (data[i + offset] << 8) | data[i + offset + 1];

                    // Check if it's in the typical port range
                    if (portBigEndian > 1023 && portBigEndian < 65536)
                        return portBigEndian;

                    // Try little-endian encoding as fallback
                    var portLittleEndian = data[i + offset] | (data[i + offset + 1] << 8);
                    if (portLittleEndian > 1023 && portLittleEndian < 65536)
                        return portLittleEndian;
                }

        return -1;
    }

    /// <summary>
    ///     Searches for MessagePack integer markers that might indicate port numbers.
    /// </summary>
    private static int FindPortByMessagePackMarkers(byte[] data)
    {
        for (var i = 0; i < data.Length - 3; i++)
            // MessagePack integer formats: uint8, uint16, int16
            if ((data[i] >= 0xCC && data[i] <= 0xCD) || // uint8/uint16 markers
                data[i] == 0xD1) // int16 marker
            {
                var port = 0;

                // uint8 format
                if (data[i] == 0xCC && i + 1 < data.Length)
                    port = data[i + 1];
                // uint16 format - big endian
                else if (data[i] == 0xCD && i + 2 < data.Length)
                    port = (data[i + 1] << 8) | data[i + 2];
                // int16 format - big endian
                else if (data[i] == 0xD1 && i + 2 < data.Length)
                    port = (data[i + 1] << 8) | data[i + 2];

                if (port > 1023 && port < 65536) // Valid port range
                    return port;
            }

        return -1;
    }

    /// <summary>
    ///     Last resort method that blindly searches for byte sequences that might represent common port numbers.
    /// </summary>
    private static int BruteForcePortSearch(byte[] data)
    {
        for (var i = 0; i < data.Length - 1; i++)
        {
            // Try big-endian
            var portBig = (data[i] << 8) | data[i + 1];

            // Check for common port ranges - prefer well-known game ranges first
            if ((portBig >= 7000 && portBig <= 8000) || // Common game server range
                (portBig >= 27000 && portBig <= 28000) || // Common game server range
                (portBig >= 5000 && portBig <= 6000)) // Common port range
                return portBig;

            // Try little-endian
            var portLittle = data[i] | (data[i + 1] << 8);
            if ((portLittle >= 7000 && portLittle <= 8000) ||
                (portLittle >= 27000 && portLittle <= 28000) ||
                (portLittle >= 5000 && portLittle <= 6000))
                return portLittle;
        }

        return -1;
    }
}