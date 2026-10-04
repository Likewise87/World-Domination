using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using TSA_WorldDomination.Settings;
using UnityEngine;
using Verse;
using WorldDomination;

namespace TSA_WorldDomination.Gizmos
{
    [HarmonyPatch(typeof(Caravan), "GetGizmos")]
    public static class Patch_SabotageGizmo
    {
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Caravan __instance)
        {
            foreach (var g in __result) yield return g;

            var seth = WorldDominationMod.settings;

            Pawn bestCrafter = __instance.PawnsListForReading
                .Where(p => p.RaceProps.Humanlike && !p.Downed && !p.Dead && !p.WorkTagIsDisabled(WorkTags.ManualSkilled))
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Crafting).Level)
                .FirstOrDefault();

            if (bestCrafter == null) yield break;
            int skill = bestCrafter.skills.GetSkill(SkillDefOf.Crafting).Level;

            List<PlanetTile> neighborTiles = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(__instance.Tile, neighborTiles);
            HashSet<int> neighborIDs = neighborTiles.Select(pt => pt.tileId).ToHashSet();

            var targets = Find.WorldObjects.Settlements
                .Where(s => neighborIDs.Contains(s.Tile) && s.Faction != null && !s.Faction.IsPlayer && WorldActions_Utils.IsWdSurfaceWorldObject(s))
                .Select(s => new { Settlement = s, Comp = s.GetComponent<CompViralSpread>() })
                .Where(x => x.Comp != null)
                .ToList();

            if (!targets.Any()) yield break;

            bool anyAvailable = targets.Any(t => !t.Comp.IsOnCooldown);

            Command_Action sabotage = new Command_Action
            {
                defaultLabel = "TSA_WD_Sab_GizmoLabel".Translate(),
                icon = anyAvailable
                    ? ContentFinder<Texture2D>.Get("UI/Commands/Sabotage")
                    : ContentFinder<Texture2D>.Get("UI/Commands/Sabotage_Cooldown"),

                action = () =>
                {
                    if (targets.Count == 1)
                    {
                        ExecuteSabotage(targets[0].Comp, __instance, bestCrafter);
                    }
                    else
                    {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        foreach (var target in targets)
                        {
                            float displayChance = GetCurrentSuccessChance(target.Comp, bestCrafter, seth);
                            string label = "TSA_WD_Sab_MenuOption".Translate(target.Settlement.LabelCap, target.Comp.tier.ToString(), displayChance.ToString("P0"));

                            if (target.Comp.IsEspionageOnCooldown)
                            {
                                options.Add(new FloatMenuOption(label + " " + "TSA_WD_OnCooldown".Translate(), null));
                            }
                            else
                            {
                                options.Add(new FloatMenuOption(label, () =>
                                {
                                    ExecuteSabotage(target.Comp, __instance, bestCrafter);
                                }));
                            }
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                }
            };

            if (!anyAvailable) sabotage.Disable("TSA_WD_Sab_DisabledAlert".Translate());

            float totalChance = GetCurrentSuccessChance(targets.First().Comp, bestCrafter, seth);
            sabotage.defaultDesc = "TSA_WD_Sab_GizmoDesc".Translate(bestCrafter.LabelShort, skill, totalChance.ToString("P0"));
            yield return sabotage;
        }

        private static float GetPawnHealthFactor(Pawn pawn, WorldDominationSettings s)
        {
            float avgCap = (pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) + pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving) + pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation) + pawn.health.capacities.GetLevel(PawnCapacityDefOf.Talking) + pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight) + pawn.health.capacities.GetLevel(PawnCapacityDefOf.Hearing)) / 6f;
            return Mathf.Lerp(1f, avgCap, s.sabotageHealthImpactWeight);
        }

        private static float GetCurrentSuccessChance(CompViralSpread comp, Pawn pawn, WorldDominationSettings s)
        {
            float baseSuccessChance = s.TotalSabWeight > 0 ? s.weightSabSuccess / s.TotalSabWeight : 0f;
            float modifiedChance = baseSuccessChance + pawn.skills.GetSkill(SkillDefOf.Crafting).Level * s.sabotageSkillSuccessWeightBonus - (int)comp.tier * s.sabotageTierSuccessWeightPenalty;
            return Mathf.Clamp(modifiedChance * GetPawnHealthFactor(pawn, s), 0.01f, 0.99f);
        }

        private static void ExecuteSabotage(CompViralSpread comp, Caravan caravan, Pawn pawn)
        {
            if (comp.IsOnCooldown) return;

            var s = WorldDominationMod.settings;
            comp.espionageCooldownUntilTick = Find.TickManager.TicksGame + Mathf.RoundToInt(s.sabotageCooldownDays * 60000f);

            float wSuccess = Mathf.Max(1f, s.weightSabSuccess + pawn.skills.GetSkill(SkillDefOf.Crafting).Level * s.sabotageSkillSuccessWeightBonus * 100f - (int)comp.tier * s.sabotageTierSuccessWeightPenalty * 100f);
            wSuccess *= GetPawnHealthFactor(pawn, s);

            float totalWeight = wSuccess + s.weightSabCleanFail + s.weightSabInjuredFail + s.weightSabFatalFail;
            float roll = Rand.Range(0f, totalWeight);

            Settlement settlement = comp.parent as Settlement;
            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();

            if (roll < wSuccess)
            {
                HandleSuccess(comp, caravan, pawn, settlement, manager, s);
            }
            else
            {
                string outcome;
                if (roll < wSuccess + s.weightSabCleanFail) outcome = "CLEAN";
                else if (roll < wSuccess + s.weightSabCleanFail + s.weightSabInjuredFail) outcome = "INJURY";
                else outcome = "FATAL";

                string saveContext = "";

                if (outcome == "INJURY")
                {
                    if (Rand.Value < pawn.skills.GetSkill(SkillDefOf.Social).Level * s.sabotageSocialCleanBonus)
                    {
                        outcome = "CLEAN";
                        saveContext = "SILVERTONGUE";
                    }
                }
                else if (outcome == "FATAL")
                {
                    int combatSkill = Mathf.Max(pawn.skills.GetSkill(SkillDefOf.Shooting).Level, pawn.skills.GetSkill(SkillDefOf.Melee).Level);
                    if (Rand.Value < combatSkill * s.sabotageCombatSurvivalBonus)
                    {
                        outcome = "INJURY";
                        saveContext = "FIGHTBACK";
                    }
                }

                ApplyFailureOutcome(outcome, saveContext, pawn, (int)comp.tier, caravan, settlement, manager);
            }
        }

        private static void HandleSuccess(CompViralSpread comp, Caravan caravan, Pawn pawn, Settlement settlement, WorldComponent_SpreadManager manager, WorldDominationSettings s)
        {
            float damage = s.sabotageBaseReduction + pawn.skills.GetSkill(SkillDefOf.Crafting).Level * s.sabotageSkillReductionBonus;
            float oldStr = comp.strength;
            comp.strength -= damage;

            if (comp.strength <= 0)
            {
                Find.LetterStack.ReceiveLetter("TSA_WD_Sab_LetterObliteratedLabel".Translate(), "TSA_WD_Sab_LetterObliteratedText".Translate(pawn.LabelShort, settlement.Label), LetterDefOf.PositiveEvent, new GlobalTargetInfo(settlement.Tile));
                manager?.AddLog(new SpreadLogEntry("TSA_WD_Sab_LogDestroyed".Translate(oldStr.ToString("F0")), caravan, settlement));
                Find.WorldObjects.Remove(settlement);
            }
            else
            {
                comp.CheckTierUpdate();
                float newStr = comp.strength;
                Find.LetterStack.ReceiveLetter("TSA_WD_Sab_LetterSuccessLabel".Translate(), "TSA_WD_Sab_LetterSuccessText".Translate(pawn.LabelShort, settlement.Label, damage.ToString("F0")), LetterDefOf.PositiveEvent, settlement);
                manager?.AddLog(new SpreadLogEntry("TSA_WD_Sab_LogSuccess".Translate(oldStr.ToString("F0"), newStr.ToString("F0")), caravan, settlement));
            }
            WorldActions_Utils.RefreshMap();
        }

        private static void ApplyFailureOutcome(string outcome, string saveContext, Pawn pawn, int tier, Caravan caravan, Settlement settlement, WorldComponent_SpreadManager manager)
        {
            string label = "TSA_WD_Sab_LetterFailedLabel".Translate();
            string text = "TSA_WD_Sab_LetterFailedTextBase".Translate(pawn.LabelShort, settlement.Label);
            LetterDef letterDef = LetterDefOf.NeutralEvent;
            string logSuffix = "TSA_WD_Sab_LogClean".Translate();

            if (outcome == "CLEAN")
            {
                text += saveContext == "SILVERTONGUE"
                    ? "TSA_WD_Sab_FailureSilvertongue".Translate()
                    : "TSA_WD_Sab_FailureClean".Translate();
            }
            else
            {
                if (settlement.Faction != null) settlement.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -30);

                if (outcome == "INJURY")
                {
                    letterDef = LetterDefOf.ThreatSmall;
                    logSuffix = "TSA_WD_Sab_LogInjured".Translate();
                    ApplyInjuries(pawn, tier);

                    if (saveContext == "FIGHTBACK")
                        text += "TSA_WD_Sab_FailureFightback".Translate();
                    else
                        text += "TSA_WD_Sab_FailureSpotted".Translate();
                }
                else // FATAL
                {
                    letterDef = LetterDefOf.Death;
                    logSuffix = "TSA_WD_Sab_LogKilled".Translate();
                    pawn.Kill(null);
                    text += "TSA_WD_Sab_FailureExecuted".Translate();
                }
            }

            Find.LetterStack.ReceiveLetter(label, text, letterDef, settlement);
            manager?.AddLog(new SpreadLogEntry("TSA_WD_Sab_LogMain".Translate(logSuffix), caravan, settlement));
        }

        private static void ApplyInjuries(Pawn pawn, int tier)
        {
            int numHits = Rand.RangeInclusive(1, 4);
            float minDmg = 5f;
            float maxDmg = 13f;

            for (int i = 0; i < numHits; i++)
            {
                var partsExternal = pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Outside).ToList();
                var hitPart = partsExternal.Any() ? partsExternal.RandomElement() : null;
                if (hitPart == null) break;
                DamageDef dmgDef = Rand.Chance(0.5f) ? DamageDefOf.Blunt : DamageDefOf.Cut;
                float finalDmg = Rand.Range(minDmg, maxDmg);
                DamageInfo dinfo = new DamageInfo(dmgDef, finalDmg, 0f, -1f, null, hitPart);
                pawn.TakeDamage(dinfo);
            }
        }
    }
}