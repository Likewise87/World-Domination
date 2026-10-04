using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;
using TSA_WorldDomination.Settings;

namespace WorldDomination
{
    // Adjust Raid Points Based on Faction Threat
    [HarmonyPatch(typeof(IncidentWorker_RaidEnemy), "TryExecuteWorker")]
    public static class Patch_RaidEnemy_AdjustPoints
    {
        [HarmonyPrefix]
        public static void Prefix(IncidentParms parms)
        {
            if (parms.faction == null || parms.faction.def.hidden) return;

            // Use the customLetterText or a specific string in the label to ensure 
            // we only multiply once.
            if (parms.customLetterLabel != null && parms.customLetterLabel.EndsWith(" ")) return;

            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
            if (manager == null) return;

            var seth = WorldDominationMod.settings;

            float rawMult = manager.GetThreatScoreFor(parms.faction);
            float appliedMult = Mathf.Min(rawMult, seth.maxThreatMultiplier);

            float originalPoints = parms.points;
            float calculatedPoints = originalPoints * appliedMult;

            parms.points = Mathf.Clamp(calculatedPoints, seth.minRaidPoints, seth.maxRaidPoints);

            // Tag the label with a trailing space to mark it as processed
            if (parms.customLetterLabel == null)
                parms.customLetterLabel = "TSA_WD_RaidLabel".Translate(parms.faction.Name) + " ";
            else
                parms.customLetterLabel += " ";

            Log.Message($"[WorldDomination] Raid Scaled: Faction: {parms.faction.Name}. Storyteller Raids Points: {originalPoints:F0}. WD Multiplier: x{appliedMult:F2}. Resulting raid points: {parms.points:F0}");
        }
    }
}