using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace SunderFolkLoggingTools;

/// <summary>
///     SunderFolk Logging Tools Plugin
///     A BepInEx plugin that provides enhanced logging capabilities for SunderFolk,
///     including QR code generation for connection information.
/// </summary>
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BasePlugin
{
    /// <summary>
    ///     Static logger instance that can be accessed throughout the plugin
    /// </summary>
    internal new static ManualLogSource Log;

    /// <summary>
    ///     Plugin entry point where initialization occurs
    /// </summary>
    public override void Load()
    {
        // Initialize the logger
        Log = base.Log;
        Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

        // Initialize configuration settings from BepInEx config file
        PluginConfig.Init(Config);

        // Apply all Harmony patches defined in the assembly
        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
    }
}