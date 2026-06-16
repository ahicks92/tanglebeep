using System;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using TangledeepAccess.Focus;
using TangledeepAccess.Native;
using TangledeepAccess.Patches;
using TangledeepAccess.Speech;
using TangledeepAccess.Util;

namespace TangledeepAccess
{
    /// <summary>
    /// BepInEx entry point. Awake does non-Unity setup only (logging, native
    /// preload, Prism init, Harmony patching). Update pumps focus announcements
    /// once per frame, so the focused menu element is spoken as focus moves.
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public partial class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "io.ahicks.tangledeepaccess";
        public const string PluginName = "Tangledeep Access";

        // PluginVersion is generated from <Version> in Directory.Build.props (see the
        // GeneratePluginVersion target) so the props file is the single source of truth.

        private PrismSpeech _speech;
        private Harmony _harmony;

        private void Awake()
        {
            LogBepInExBackend.Install(Logger);
            Log.Info(PluginName + " " + PluginVersion + " loading");

            // Native preload + Prism init are pure native work (no Unity state), safe here.
            string pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (NativeLoader.LoadPrism(pluginDir))
            {
                _speech = new PrismSpeech();
                _speech.Initialize();
            }
            FocusAnnouncer.Install(_speech);

            try
            {
                _harmony = new Harmony(PluginGuid);
                _harmony.PatchAll(typeof(UIManagerScript_ChangeUIFocus_Patch));
                Log.Info("Harmony patches applied");
            }
            catch (Exception e)
            {
                Log.Error("Harmony patch failed: " + e);
            }
        }

        private void Update()
        {
            FocusAnnouncer.Pump();
        }
    }
}
