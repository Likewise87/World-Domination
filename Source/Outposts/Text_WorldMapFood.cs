using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Outposts;
using TSA_WorldDomination;
using TSA_WorldDomination.Settings;

namespace WorldDomination
{
    public class WorldLogisticsVisualizer : WorldComponent
    {
        public WorldLogisticsVisualizer(World world) : base(world) { }

        public override void WorldComponentOnGUI()
        {
            base.WorldComponentOnGUI();

            // 1. Context & Global Toggle Check
            // Gated behind both the mod settings and the world-map UI toggle
            if (Current.ProgramState != ProgramState.Playing || !WorldRendererUtility.WorldRendered) return;
            if (!WorldComponent_WDVisualizerToggle.ShowVisuals) return;
            if (WorldDominationMod.settings == null || !WorldDominationMod.settings.foodLogisticsActive) return;

            // 2. Zoom Check: Hide if zoomed out too far
            if (Find.WorldCameraDriver.AltitudePercent > 0.08f) return;

            var manager = Find.World.GetComponent<WorldComponent_LogisticsManager>();
            if (manager == null) return;

            foreach (WorldObject worldObject in Find.WorldObjects.AllWorldObjects)
            {
                // Only target player-owned outposts with the logistics component
                if (worldObject.Faction == null || !worldObject.Faction.IsPlayer) continue;

                CompOutpostLogistics logi = worldObject.GetComponent<CompOutpostLogistics>();
                if (logi == null) continue;

                // Occlusion check (don't draw if behind the planet)
                if (WorldObjectSelectionUtility.HiddenBehindTerrainNow(worldObject)) continue;

                // --- LOGIC CALCULATIONS ---
                float prod = manager.GetDailyProduction(worldObject);
                float cons = manager.GetDailyDemand(worldObject);
                float netBase = prod - cons;

                // Calculate real-time transfers
                float outgoing = manager.manualLinks.Where(l => l.sourceTile == worldObject.Tile).Sum(l => l.manualAssignment);
                float incoming = manager.manualLinks.Where(l => l.destTile == worldObject.Tile).Sum(l => l.manualAssignment * manager.GetEfficiency(l.sourceTile, l.destTile));

                float totalNet = netBase + incoming - outgoing;

                // --- UI PREPARATION ---
                Vector2 screenPos = WorldObjectSelectionUtility.ScreenPos(worldObject);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;

                // Top Line: Deficit/Surplus
                string netSign = totalNet >= 0 ? "+" : "";
                string netLabel = $"{netSign}{totalNet:F1}";
                Color netColor = totalNet > 0.1f ? Color.green : (totalNet < -0.1f ? Color.red : Color.yellow);

                // Bottom Line: Current Food
                string foodLabel = $"{logi.currentFood:F0}/100";
                Color foodColor = logi.currentFood <= 20f ? Color.red : (logi.currentFood <= 50f ? Color.yellow : Color.white);

                // --- DRAWING ---
                float yOffset = -45f;
                Rect netRect = new Rect(screenPos.x - 50f, screenPos.y + yOffset, 100f, 20f);
                Rect foodRect = new Rect(screenPos.x - 50f, screenPos.y + yOffset + 12f, 100f, 20f);

                // Shadow Pass
                GUI.color = new Color(0, 0, 0, 0.7f);
                Widgets.Label(new Rect(netRect.x + 1f, netRect.y + 1f, netRect.width, netRect.height), netLabel);
                Widgets.Label(new Rect(foodRect.x + 1f, foodRect.y + 1f, foodRect.width, foodRect.height), foodLabel);

                // Main Label Pass
                GUI.color = netColor;
                Widgets.Label(netRect, netLabel);
                GUI.color = foodColor;
                Widgets.Label(foodRect, foodLabel);

                // Cleanup
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
        }
    }
}