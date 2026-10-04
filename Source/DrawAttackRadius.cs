using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using TSA_WorldDomination.Settings;
using WorldDomination;

namespace TSA_WorldDomination
{
    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.DrawExtraSelectionOverlays))]
    public static class Patch_WorldObject_DrawAttackRadius
    {
        [HarmonyPostfix]
        public static void Postfix(WorldObject __instance)
        {
            if (!Find.WorldSelector.IsSelected(__instance)) return;

            if (!(__instance is Settlement settlement) || settlement.Faction == null || settlement.Faction.IsPlayer)
                return;

            if (settlement.Faction.def.hidden) return;

            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
            if (manager == null) return;

            if (!WorldActions_Utils.IsWdSurfaceTile(settlement.Tile))
                return;

            var seth = WorldDominationMod.settings;
            float range = seth.raidTargetRadius;

            // --- SURGICAL RADIUS UPDATE ---
            bool hasZeal = settlement.Faction == manager.expansionistZealFaction &&
                           Find.TickManager.TicksGame < manager.expansionistZealExpiryTick;

            if (hasZeal)
            {
                range *= 2f; // Match the 100% increase in AttemptRaid
            }

            float visualAdjustedRange = range * 1.15f;

            // Draw logic: If they have zeal, pulse the color or set it to Cyan to match the UI tag
            if (hasZeal)
            {
                // Temporarily set GUI color for the radius ring draw
                Color originalColor = GUI.color;
                GUI.color = Color.cyan;
                GenDraw.DrawWorldRadiusRing(settlement.Tile, Mathf.CeilToInt(visualAdjustedRange));
                GUI.color = originalColor;
            }
            else
            {
                GenDraw.DrawWorldRadiusRing(settlement.Tile, Mathf.CeilToInt(visualAdjustedRange));
            }
        }
    }
}