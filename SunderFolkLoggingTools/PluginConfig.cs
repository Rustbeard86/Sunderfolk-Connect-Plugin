﻿using BepInEx.Configuration;

namespace SunderFolkLoggingTools;

/// <summary>
///     Manages configuration settings for the SunderFolk Logging Tools plugin.
///     Provides central access to user-configurable options that control
///     debug logging and QR code image generation functionality.
/// </summary>
internal static class PluginConfig
{
    /// <summary>
    ///     When enabled, outputs detailed diagnostic information about QR codes,
    ///     network connections, and MessagePack data manipulations.
    ///     Useful for debugging network connection issues.
    /// </summary>
    public static ConfigEntry<bool> DevMode;

    /// <summary>
    ///     When enabled, generates QR code images for connection URLs and
    ///     opens them using the system's default image viewer.
    ///     This allows easy scanning of connection information with mobile devices.
    /// </summary>
    public static ConfigEntry<bool> EnableQrImageGeneration;

    /// <summary>
    ///     Controls the amount of detail in logs. Basic info if disabled, more details if enabled.
    /// </summary>
    public static ConfigEntry<bool> VerboseLogging;

    /// <summary>
    ///     Enables lowest level debug information including raw data dumps.
    /// </summary>
    public static ConfigEntry<bool> DebugLogging;

    /// <summary>
    ///     Initializes all configuration entries from the BepInEx configuration system.
    ///     Should be called once during plugin startup.
    /// </summary>
    /// <param name="config">The BepInEx configuration file to bind settings to</param>
    public static void Init(ConfigFile config)
    {
        DevMode = config.Bind(
            "General",
            "DevMode",
            false,
            "Enable verbose debug logging for network activity and QR processing."
        );

        EnableQrImageGeneration = config.Bind(
            "General",
            "GenerateQrImage",
            false,
            "Generate and open QR PNG files for connection URLs. Useful for sharing connections with mobile devices."
        );

        VerboseLogging = config.Bind(
            "Logging",
            "Verbose",
            false,
            "Enable detailed logging of operations. Requires DevMode enabled."
        );

        DebugLogging = config.Bind(
            "Logging",
            "Debug",
            false,
            "Enable raw data inspection and byte-level logging. Requires DevMode enabled."
        );
    }
}