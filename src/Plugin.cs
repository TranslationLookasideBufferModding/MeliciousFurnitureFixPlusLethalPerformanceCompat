using System;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;

namespace MliciosFurnitureFixPlus
{
    [BepInPlugin(Id, "MliciosFurniture Fix+", VersionInfo.Version)]
    [BepInDependency("MelanieMelicious.furniturePack0", "2.0.5")]
    [BepInDependency("imabatby.lethallevelloader", "1.7.13")]
    [BepInDependency("com.github.teamxiaolan.dawnlib", BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "TranslationLookasideBufferModding.MliciosFurnitureFixPlus";
        private Harmony harmony;
        private float nextCheck;
        private void Awake()
        {
            if (!Config.Bind("General", "Enabled", true, "Repair the Utility Furniture Pack ship fire exit. Install on ALL players. Restart required.").Value) return;
            try
            {
                RequireVersion("MelanieMelicious.furniturePack0", "2.0.5");
                RequireVersion("imabatby.lethallevelloader", "1.7.13");
                Runtime.Log = delegate(string text) { Logger.LogInfo(text); };
                Runtime.Warn = delegate(string text) { Logger.LogWarning(text); };
                harmony = new Harmony(Id);
                Patches.Install(harmony);
                Logger.LogInfo("MliciosFurnitureFixPlus " + VersionInfo.Version + " active: LLL and DawnLib generation paths, no furniture reparenting, fresh exit-ID scan and explicit ship/interior pairing. LethalPerformance can stay enabled. Same addon required on every player.");
            }
            catch (Exception e)
            {
                if (harmony != null) harmony.UnpatchSelf();
                Logger.LogError("MliciosFurnitureFixPlus could not initialize; original behavior retained: " + e);
                enabled = false;
            }
        }
        private static void RequireVersion(string guid, string version)
        {
            PluginInfo info;
            if (!Chainloader.PluginInfos.TryGetValue(guid, out info) || info.Metadata.Version.ToString() != version)
                throw new NotSupportedException("Expected " + guid + " " + version + ".");
        }
        private void Update()
        {
            if (TimeNow() < nextCheck) return;
            nextCheck = TimeNow() + 0.5f;
            Runtime.UpdateInteraction(); // Only cached doors; never a scene scan per frame.
        }
        private static float TimeNow() { return UnityEngine.Time.unscaledTime; }
    }
}
