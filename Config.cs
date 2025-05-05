// Config.cs
using BepInEx;
using BepInEx.Configuration;

namespace SunderFolkLoggingTools
{
    internal static class Config
    {
        public static ConfigEntry<bool> DevMode;
        public static ConfigEntry<bool> EnableQrImageGeneration;
        public static ConfigEntry<bool> EnablePatchLogic;

        public static void Init(ConfigFile config)
        {
            DevMode = config.Bind("General", "DevMode", false, "Enable verbose debug logging.");
            EnableQrImageGeneration = config.Bind("General", "GenerateQrImage", true, "Generate and open QR PNG files.");
            EnablePatchLogic = config.Bind("General", "EnablePatchLogic", true, "Enable patching of IP in QR join URLs.");
        }
    }
}