using System;
using SunderFolkLoggingTools.Shared;
using SunderFolkLoggingTools.Tools;

namespace SunderFolkLoggingTools.Bridges;

/// <summary>
///     Bridge class that handles transferring data between IL2CPP and managed code
///     for MessagePack operations
/// </summary>
public static class MessagePackBridge
{
    /// <summary>
    ///     Takes base64 data from IL2CPP side, processes it with MessagePackDebugTool,
    ///     and returns results without exposing MessagePack types directly
    /// </summary>
    public static BridgeResult ProcessJoinParameter(string base64Input, string externalIP)
    {
        return LoggingHelper.LogOperation("ProcessJoinParameter", () =>
        {
            try
            {
                // Use MessagePackDebugTool to decode
                var joinData = MessagePackDebugTool.DecodeFromBase64(base64Input);
                LoggingHelper.Logger.Info("Successfully decoded MessagePack data");

                // Track replaced IPs
                var replacedAny = false;
                string originalIp = null;
                var port = -1;

                // Process IP replacement
                if (joinData?.ConnectionGroups != null)
                    foreach (var group in joinData.ConnectionGroups)
                    foreach (var bufferPair in group)
                        if (MessagePackDebugTool.IsPrivateIP(bufferPair.Data))
                        {
                            // Store original IP
                            if (originalIp == null)
                            {
                                originalIp =
                                    $"{bufferPair.Data[0]}.{bufferPair.Data[1]}.{bufferPair.Data[2]}.{bufferPair.Data[3]}";
                                port = bufferPair.Port;
                            }

                            // Replace IP with external one
                            var ipParts = externalIP.Split('.');
                            if (ipParts.Length == 4)
                            {
                                for (var i = 0; i < 4; i++)
                                    if (byte.TryParse(ipParts[i], out var octet))
                                        bufferPair.Data[i] = octet;
                                replacedAny = true;
                            }
                        }

                // Log IP replacement
                if (replacedAny && originalIp != null)
                    LoggingHelper.LogIpReplacement(originalIp, externalIP, port);

                // Only re-encode if modified
                var modifiedBase64 = replacedAny ? MessagePackDebugTool.EncodeToBase64(joinData) : base64Input;

                // Debug JSON dump for verbose logging
                LoggingHelper.LogMessagePackData(modifiedBase64);

                return new BridgeResult
                {
                    Success = true,
                    ModifiedBase64 = modifiedBase64,
                    DetectedPort = port,
                    DidReplaceIP = replacedAny
                };
            }
            catch (Exception ex)
            {
                LoggingHelper.LogException("ProcessJoinParameter", ex);
                return new BridgeResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        });
    }

    /// <summary>
    ///     Analyzes and logs MessagePack join data in a human-readable format
    /// </summary>
    public static void AnalyzeJoinData(string base64Input)
    {
        LoggingHelper.LogOperation("Analyze Join Data", () =>
        {
            try
            {
                // Decode the join data
                var joinData = MessagePackDebugTool.DecodeFromBase64(base64Input);

                // Extract useful information
                if (joinData?.ConnectionGroups != null && joinData.ConnectionGroups.Count > 0)
                {
                    var firstGroup = joinData.ConnectionGroups[0];
                    if (firstGroup.Count > 0)
                    {
                        var firstConnection = firstGroup[0];
                        if (firstConnection.Data.Length >= 4)
                        {
                            var ipString =
                                $"{firstConnection.Data[0]}.{firstConnection.Data[1]}.{firstConnection.Data[2]}.{firstConnection.Data[3]}";
                            var port = firstConnection.Port;

                            // Log network details with new style
                            LoggingHelper.Logger.Info("Network Connection Info:");
                            LoggingHelper.Logger.Info($"• Local IP: {ipString}");
                            LoggingHelper.Logger.Info($"• External IP: {QrUtilities.GetExternalIpAddress()}");
                            LoggingHelper.Logger.Info($"• Port: {port}");
                        }
                    }
                }

                // Use the new message pack logging method
                LoggingHelper.LogMessagePackData(base64Input);
            }
            catch (Exception ex)
            {
                LoggingHelper.Logger.Error($"Failed to analyze join data: {ex.Message}");
            }
        });
    }

    /// <summary>
    ///     Simple data class for returning results from MessagePack operations
    ///     without exposing MessagePack types
    /// </summary>
    public class BridgeResult
    {
        public bool Success { get; set; }
        public string ModifiedBase64 { get; set; }
        public string ErrorMessage { get; set; }
        public int DetectedPort { get; set; } = -1;
        public bool DidReplaceIP { get; set; }
    }
}