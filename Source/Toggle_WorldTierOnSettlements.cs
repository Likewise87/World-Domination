using System;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WorldDomination
{
    [StaticConstructorOnStartup]
    public class WorldComponent_WDVisualizerToggle : WorldComponent
    {
        public static bool ShowVisuals = true;
        private static readonly Texture2D IconTex = TexButton.OpenStatsReport;

        static WorldComponent_WDVisualizerToggle()
        {
            var harmony = new Harmony("TSA.WorldDomination.PlaySettingsPatch");
            harmony.Patch(
                AccessTools.Method(typeof(GlobalControlsUtility), nameof(GlobalControlsUtility.DoPlaySettings)),
                prefix: new HarmonyMethod(typeof(WorldComponent_WDVisualizerToggle), nameof(Prefix_DoPlaySettings))
            );
        }

        public WorldComponent_WDVisualizerToggle(World world) : base(world) { }

        // Parameter name changed to rowVisibility to match RimWorld source exactly
        public static void Prefix_DoPlaySettings(WidgetRow rowVisibility, bool worldView)
        {
            if (worldView)
            {
                rowVisibility.ToggleableIcon(ref ShowVisuals, IconTex, "Toggle World Domination Logistics/Tier Overlay", SoundDefOf.Tick_High);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref ShowVisuals, "ShowWDVisuals", true);
        }

        public override void WorldComponentOnGUI() { }
    }
}