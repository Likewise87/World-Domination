using HarmonyLib;
using Verse;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;

namespace WorldDomination
{
    [StaticConstructorOnStartup]
    public static class VassalCompatibility
    {
        static VassalCompatibility()
        {
            var harmony = new Harmony("TSA.WorldDomination.VassalCompat");

            try
            {
                // Check if the Vassalise mod is present
                Type vassalUtility = AccessTools.TypeByName("FactionTerritories.Vassalise.VassaliseUtility");
                if (vassalUtility != null)
                {
                    // 1. Inject CompViralSpread into the Vassal Def at runtime
                    var vassalDef = DefDatabase<WorldObjectDef>.GetNamedSilentFail("FT_VassalOutpost");
                    if (vassalDef != null)
                    {
                        if (vassalDef.comps == null) vassalDef.comps = new List<WorldObjectCompProperties>();
                        vassalDef.comps.Add(new CompProperties_ViralSpread());
                        Log.Message("[WorldDomination] Successfully injected CompViralSpread into FT_VassalOutpost.");
                    }

                    // 2. Manually patch the ExecuteVassalisation method
                    var targetMethod = AccessTools.Method(vassalUtility, "ExecuteVassalisation");
                    if (targetMethod != null)
                    {
                        harmony.Patch(targetMethod, postfix: new HarmonyMethod(typeof(Patch_VassalCompat), nameof(Patch_VassalCompat.VassalInitPostfix)));
                        Log.Message("[WorldDomination] Applied postfix to ExecuteVassalisation.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning("[WorldDomination] Failed to initialize Vassal Compatibility: " + ex.Message);
            }
        }
    }

    public static class Patch_VassalCompat
    {
        public static void VassalInitPostfix(Settlement settlement, bool __result)
        {
            // If the method returned true, a vassal outpost was just created on this tile
            if (__result && settlement != null)
            {
                // Use non-generic ObjectsAt to find the newly spawned world object
                var objectsAtTile = Find.WorldObjects.ObjectsAt(settlement.Tile);

                foreach (var obj in objectsAtTile)
                {
                    // Identify the object by its specific class name
                    if (obj.GetType().FullName == "FactionTerritories.Vassalise.FactionTerritories_VassalOutpost")
                    {
                        var comp = obj.GetComponent<CompViralSpread>();
                        if (comp != null)
                        {
                            comp.subType = "Vassal";
                            comp.strength = 400f;
                            Log.Message($"[WorldDomination] Initialized Vassal Comp at tile {settlement.Tile}");
                        }
                        break;
                    }
                }
            }
        }
    }
}