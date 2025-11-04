namespace StationeersIC10Editor
{
    using UnityEngine;
    using System;
    using BepInEx;
    using BepInEx.Configuration;
    using HarmonyLib;
    using System.Collections;

    class L
    {
        private static BepInEx.Logging.ManualLogSource _logger;

        public static void SetLogger(BepInEx.Logging.ManualLogSource logger)
        {
            _logger = logger;
        }

        public static void Info(string message)
        {
            _logger?.LogInfo(message);
        }

        public static void Error(string message)
        {
            _logger?.LogError(message);
        }

        public static void Warning(string message)
        {
            _logger?.LogWarning(message);
        }

    }

    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class IC10EditorPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "aproposmath-stationeers-ic10-editor"; // Change this to your own unique Mod ID
        public const string PluginName = "IC10Editor";
        public const string PluginVersion = VersionInfo.Version;
        private Harmony _harmony;

        public static ConfigEntry<bool> VimBindings;
        public static ConfigEntry<bool> EnforceLineLimit;
        public static ConfigEntry<bool> EnforceBytesLimit;

        private void BindAllConfigs()
        {
            VimBindings = Config.Bind("General", "Enable VIM bindings (experimental!)", false, "Enable VIM bindings, this is an experimental feature");
            EnforceLineLimit = Config.Bind("General", "Enforce 128 line limit", true, "Enforce the 128 line limit of IC10 programs");
            EnforceBytesLimit = Config.Bind("General", "Enforce 4KB size limit", true, "Enforce the 4KB byte size of IC10 programs");
        }

        private void Awake()
        {
            try
            {
                L.SetLogger(this.Logger);
                this.Logger.LogInfo(
                    $"Awake ${PluginName} {VersionInfo.VersionGit}, build time {VersionInfo.BuildTime}");
                BindAllConfigs();

                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll();
            }
            catch (Exception ex)
            {
                this.Logger.LogError($"Error during ${PluginName} {VersionInfo.VersionGit} init: {ex}");
            }
        }

        private void OnDestroy()
        {
            L.Info($"OnDestroy ${PluginName} {VersionInfo.VersionGit}");
            IC10EditorPatches.Cleanup();
            _harmony.UnpatchSelf();
        }
    }
}
