using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using TSA_WorldDomination.Settings;
using Verse;

namespace WorldDomination
{
    [StaticConstructorOnStartup]
    public static class KCSG_Integration_Init
    {
        static KCSG_Integration_Init()
        {
            var harmony = new Harmony("TSA.WorldDomination.KCSG_Final_Solution");
            harmony.PatchAll();

            var genDefMethod = AccessTools.PropertyGetter(typeof(Settlement), "MapGeneratorDef");
            if (genDefMethod != null)
            {
                harmony.Patch(genDefMethod, postfix: new HarmonyMethod(typeof(KCSG_Integration_Patch), nameof(KCSG_Integration_Patch.ForceKCSGGeneratorPostfix)));
            }

            Type customGenType = AccessTools.TypeByName("KCSG.CustomGenOption");
            if (customGenType != null)
            {
                var targetMethod = AccessTools.Method(customGenType, "Generate");
                if (targetMethod != null)
                {
                    harmony.Patch(targetMethod, prefix: new HarmonyMethod(typeof(KCSG_Integration_Patch), nameof(KCSG_Integration_Patch.LayoutOverridePrefix)));
                    Log.Message("[WorldDomination] KCSG Integration: Successfully hooked generator logic and ran PatchAll.");
                }
            }
        }
    }

    public static class KCSG_Integration_Patch
    {

        public static void ForceKCSGGeneratorPostfix(Settlement __instance, ref MapGeneratorDef __result)
        {
            if (__instance == null || __instance.Faction == null || __instance.Faction.def == null || __instance.Faction.IsPlayer) return;
            if (!WorldActions_Utils.IsWdSurfaceTile(__instance.Tile)) return;

            // SIMPLE EXCLUSION CHECK
            string fName = __instance.Faction.Name.ToLower();
            string dName = __instance.Faction.def.defName.ToLower();

            if (fName.Contains("insect") || dName.Contains("insect") || fName.Contains("hive") || dName.Contains("hive"))
            {
                Log.Message($"[WorldDomination] KCSG: Skipping {__instance.LabelCap} - Faction {__instance.Faction.Name} matches exclusion strings.");
                return;
            }

            // QUEST EXCLUSION: Don't hijack if the settlement is part of an active quest
            if (WorldActions_Utils.HasActiveQuest(__instance))
            {
                Log.Message($"[WorldDomination] KCSG: Skipping {__instance.LabelCap} - Active quest detected.");
                return;
            }

            if (__instance.GetComponent<CompViralSpread>() == null) return;

            Log.Message($"[WorldDomination] KCSG: Proceeding with hijack for valid faction: {__instance.Faction.Name}");

            try
            {
                var kcsgGen = DefDatabase<MapGeneratorDef>.GetNamed("KCSG_Base_Faction", false);
                if (kcsgGen != null)
                {
                    __result = kcsgGen;

                    Type extensionType = AccessTools.TypeByName("KCSG.CustomGenOption");
                    if (extensionType != null)
                    {
                        if (__instance.Faction.def.modExtensions == null)
                        {
                            __instance.Faction.def.modExtensions = new List<DefModExtension>();
                        }

                        if (!__instance.Faction.def.modExtensions.Any(ex => ex != null && extensionType.IsAssignableFrom(ex.GetType())))
                        {
                            var newExtension = (DefModExtension)Activator.CreateInstance(extensionType);
                            FieldInfo listField = AccessTools.Field(extensionType, "chooseFromSettlements");
                            if (listField != null)
                            {
                                Type layoutDefType = AccessTools.TypeByName("KCSG.SettlementLayoutDef");
                                if (layoutDefType != null)
                                {
                                    var emptyList = Activator.CreateInstance(typeof(List<>).MakeGenericType(layoutDefType));
                                    listField.SetValue(newExtension, emptyList);
                                }
                            }
                            __instance.Faction.def.modExtensions.Add(newExtension);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"[WorldDomination] KCSG Hijack failed gracefully for {__instance.LabelCap}. Error: {ex.Message}");
            }
        }

        public static void LayoutOverridePrefix(object __instance, Map map)
        {
            if (map?.Parent == null || !(map.Parent is Settlement settlement)) return;
            if (settlement.Faction == null || settlement.Faction.IsPlayer) return;
            if (!WorldActions_Utils.IsWdSurfaceTile(settlement.Tile)) return;

            // SIMPLE EXCLUSION CHECK
            string fName = settlement.Faction.Name.ToLower();
            string dName = settlement.Faction.def.defName.ToLower();
            if (fName.Contains("insect") || dName.Contains("insect") || fName.Contains("hive") || dName.Contains("hive")) return;

            // QUEST EXCLUSION
            if (WorldActions_Utils.HasActiveQuest(settlement))
            {
                Log.Message($"[WorldDomination] KCSG: Active quest at map. Leaving it be for {settlement.LabelCap}.");
                return;
            }

            var spreadComp = settlement.GetComponent<CompViralSpread>();
            if (spreadComp == null) return;

            Type genType = __instance.GetType();

            // Set basic KCSG flags
            FieldInfo bridgeField = AccessTools.Field(genType, "preventBridgeable");
            if (bridgeField != null) bridgeField.SetValue(__instance, false);

            FieldInfo randomLocField = AccessTools.Field(genType, "spawnInRandomFreeLocation");
            if (randomLocField != null) randomLocField.SetValue(__instance, false);

            bool isTribal = settlement.Faction.def.techLevel <= TechLevel.Medieval;
            string techPrefix = isTribal ? "Tribal" : "Generic";
            string tier = spreadComp.tier.ToString();
            string baseType = (spreadComp.tier == SettlementTier.T4) ? "Citadel" : spreadComp.subType;

            string specificPattern = $"TSA_{techPrefix}_{tier}_{baseType}";
            string fallbackPattern = $"TSA_{techPrefix}_{tier}";

            Type layoutDefType = AccessTools.TypeByName("KCSG.SettlementLayoutDef");
            if (layoutDefType == null) return;

            Type dbType = typeof(DefDatabase<>).MakeGenericType(layoutDefType);
            var allLayouts = (IEnumerable<Def>)AccessTools.Property(dbType, "AllDefs").GetValue(null);
            if (allLayouts == null) return;

            var validLayouts = allLayouts.Where(d => d != null && d.defName.StartsWith(specificPattern)).ToList();
            if (!validLayouts.Any())
            {
                validLayouts = allLayouts.Where(d => d != null && d.defName.StartsWith(fallbackPattern)).ToList();
            }

            if (validLayouts.Any())
            {
                var chosen = validLayouts.RandomElement();
                if (chosen != null)
                {
                    // --- SURGICAL INJECTION: Apply Garrison Multiplier from Settings ---
                    var s = WorldDominationMod.settings;
                    float targetMult = 1f;

                    if (isTribal)
                    {
                        if (spreadComp.tier == SettlementTier.T1) targetMult = s.kcsgMultTribalT1;
                        else if (spreadComp.tier == SettlementTier.T2) targetMult = s.kcsgMultTribalT2;
                        else if (spreadComp.tier == SettlementTier.T3) targetMult = s.kcsgMultTribalT3;
                        else if (spreadComp.tier == SettlementTier.T4) targetMult = s.kcsgMultTribalT4;
                    }
                    else
                    {
                        if (spreadComp.tier == SettlementTier.T1) targetMult = s.kcsgMultGenericT1;
                        else if (spreadComp.tier == SettlementTier.T2) targetMult = s.kcsgMultGenericT2;
                        else if (spreadComp.tier == SettlementTier.T3) targetMult = s.kcsgMultGenericT3;
                        else if (spreadComp.tier == SettlementTier.T4) targetMult = s.kcsgMultGenericT4;
                    }

                    // Drill down into DefenseOptions -> pawnGroupMultiplier
                    FieldInfo defenseField = AccessTools.Field(chosen.GetType(), "defenseOptions");
                    if (defenseField != null)
                    {
                        object defenseObj = defenseField.GetValue(chosen);
                        if (defenseObj != null)
                        {
                            FieldInfo multField = AccessTools.Field(defenseObj.GetType(), "pawnGroupMultiplier");
                            multField?.SetValue(defenseObj, targetMult);
                        }
                    }

                    // Finalize the choice for KCSG
                    FieldInfo listField = AccessTools.Field(genType, "chooseFromSettlements");
                    if (listField != null)
                    {
                        var newList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(layoutDefType));
                        newList.Add(chosen);
                        listField.SetValue(__instance, newList);
                    }
                }
            }
        }
    }
}