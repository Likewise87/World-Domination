using HarmonyLib;
using Outposts;
using Verse;
using WorldDomination;
using TSA_WorldDomination;

namespace TSA_WorldDomination.HarmonyPatches
{
    // --- OUTPOST ROSTER EVENTS ---
    // These ensure that when a player adds or removes a pawn, the outpost's strength
    // is immediately recalculated to reflect the new combat potential.

    [HarmonyPatch(typeof(Outpost), nameof(Outpost.AddPawn))]
    public static class Patch_Outpost_AddPawn
    {
        [HarmonyPostfix]
        public static void Postfix(Outpost __instance, bool __result)
        {
            // If the Outpost successfully added the pawn, trigger a strength update
            if (__result)
            {
                var comp = __instance.GetComponent<CompViralSpread>();
                comp?.UpdateOutpostStrengthLogically();
                Find.World?.GetComponent<WorldComponent_LogisticsManager>()?.NotifyFoodLogisticsInputsChanged();
            }
        }
    }

    [HarmonyPatch(typeof(Outpost), nameof(Outpost.RemovePawn))]
    public static class Patch_Outpost_RemovePawn
    {
        [HarmonyPostfix]
        public static void Postfix(Outpost __instance)
        {
            // Triggered whenever a pawn is removed from the roster (manually or via death/transfer)
            var comp = __instance.GetComponent<CompViralSpread>();
            comp?.UpdateOutpostStrengthLogically();
            Find.World?.GetComponent<WorldComponent_LogisticsManager>()?.NotifyFoodLogisticsInputsChanged();
        }
    }
}