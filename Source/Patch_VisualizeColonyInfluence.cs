using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using WorldDomination;

namespace TSA_WorldDomination.Patches
{
    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.DrawExtraSelectionOverlays))]
    public static class Patch_SettlementInfluence
    {
        public static void Postfix(WorldObject __instance)
        {
            if (__instance is Settlement settlement && settlement.Faction?.IsPlayer == true)
            {
                float radius = WorldActions_Utils.GetPlayerInfluenceRadius(settlement.Tile);

                if (radius > 0)
                {
                    int roundedRadius = Mathf.CeilToInt(radius) + 1;
                    GenDraw.DrawWorldRadiusRing(settlement.Tile, roundedRadius);
                }
            }
        }
    }
}