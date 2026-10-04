using System;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace WorldDomination
{
    public class Text_WorldTierOnSettlements : WorldComponent
    {
        public Text_WorldTierOnSettlements(World world) : base(world) { }

        public override void WorldComponentOnGUI()
        {
            base.WorldComponentOnGUI();

            // 1. Check if we are in playing state and rendering the world
            if (Current.ProgramState != ProgramState.Playing || !WorldRendererUtility.WorldRendered)
            {
                return;
            }

            // NEW GATE: Respect the World Map toggle
            if (!WorldComponent_WDVisualizerToggle.ShowVisuals)
            {
                return;
            }

            if (Find.WorldCameraDriver.AltitudePercent > 0.25f)
            {
                return;
            }

            foreach (WorldObject worldObject in Find.WorldObjects.AllWorldObjects)
            {
                CompViralSpread comp = worldObject.GetComponent<CompViralSpread>();


                if (comp == null || comp.subType == "Excluded") continue;

                if (worldObject.Faction != null && worldObject.Faction.IsPlayer) continue;
                if (comp.IsOutpost) continue;

                if (WorldObjectSelectionUtility.HiddenBehindTerrainNow(worldObject))
                {
                    continue;
                }

                string labelText = comp.tier.ToString();
                Vector2 screenPos = WorldObjectSelectionUtility.ScreenPos(worldObject);

                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;

                Color labelColor = GetColorForTier(comp.tier);
                float textSize = Text.CalcSize(labelText).x;
                float yOffset = -32f;

                Rect rect = new Rect(screenPos.x - textSize / 2f, screenPos.y + yOffset, textSize, 20f);

                // Draw Shadow
                Color originalColor = GUI.color;
                GUI.color = new Color(0, 0, 0, 0.8f);
                Rect shadowRect = new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height);
                Widgets.Label(shadowRect, labelText);

                // Draw Main Label
                GUI.color = labelColor;
                Widgets.Label(rect, labelText);

                // Cleanup
                GUI.color = originalColor;
                Text.Anchor = TextAnchor.UpperLeft;
                Text.Font = GameFont.Small;
            }
        }

        private Color GetColorForTier(SettlementTier tier)
        {
            switch (tier)
            {
                case SettlementTier.T4: return new Color(0.8f, 0.2f, 1f); // Purple/Magenta for T4
                case SettlementTier.T3: return new Color(1f, 0.3f, 0.3f);
                case SettlementTier.T2: return new Color(1f, 0.8f, 0.2f);
                case SettlementTier.T1: return Color.white;
                default: return Color.gray;
            }
        }
    }
}