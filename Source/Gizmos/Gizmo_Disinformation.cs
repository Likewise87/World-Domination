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
    public static class Patch_DisinformationGizmo
    {
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Caravan __instance)
        {
            foreach (var g in __result) yield return g;

            var seth = WorldDominationMod.settings;

            // 1. Find the best face (Social Skill)
            Pawn bestDiplomat = __instance.PawnsListForReading
                .Where(p => p.RaceProps.Humanlike && !p.Downed && !p.Dead && !p.WorkTagIsDisabled(WorkTags.Social))
                .OrderByDescending(p => p.skills.GetSkill(SkillDefOf.Social).Level)
                .FirstOrDefault();

            if (bestDiplomat == null) yield break;
            int skill = bestDiplomat.skills.GetSkill(SkillDefOf.Social).Level;

            // 2. Scan neighbors
            List<PlanetTile> neighborTiles = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(__instance.Tile, neighborTiles);
            HashSet<int> neighborIDs = neighborTiles.Select(pt => pt.tileId).ToHashSet();

            var targets = Find.WorldObjects.Settlements
                .Where(s => neighborIDs.Contains(s.Tile) && s.Faction != null && !s.Faction.IsPlayer && WorldActions_Utils.IsWdSurfaceWorldObject(s))
                .Select(s => new { Settlement = s, Comp = s.GetComponent<CompViralSpread>() })
                .Where(x => x.Comp != null)
                .ToList();

            if (!targets.Any()) yield break;

            bool anyAvailable = targets.Any(t => !t.Comp.IsEspionageOnCooldown);

            // 3. Create the Gizmo
            Command_Action disinformation = new Command_Action
            {
                defaultLabel = "TSA_WD_Dis_GizmoLabel".Translate(),
                icon = anyAvailable
                    ? ContentFinder<Texture2D>.Get("UI/Commands/Disinformation")
                    : ContentFinder<Texture2D>.Get("UI/Commands/Disinformation_Cooldown"),
                action = () =>
                {
                    if (targets.Count == 1)
                    {
                        ExecuteDisinformation(targets[0].Comp, __instance, bestDiplomat);
                    }
                    else
                    {
                        List<FloatMenuOption> options = new List<FloatMenuOption>();
                        foreach (var target in targets)
                        {
                            float displayChance = GetCurrentDisChance(target.Comp, bestDiplomat, seth);
                            string label = "TSA_WD_Dis_MenuOption".Translate(target.Settlement.LabelCap, target.Comp.tier.ToString(), displayChance.ToString("P0"));

                            if (target.Comp.IsEspionageOnCooldown)
                            {
                                options.Add(new FloatMenuOption(label + " " + "TSA_WD_OnCooldown".Translate(), null));
                            }
                            else
                            {
                                options.Add(new FloatMenuOption(label, () =>
                                {
                                    ExecuteDisinformation(target.Comp, __instance, bestDiplomat);
                                }));
                            }
                        }
                        Find.WindowStack.Add(new FloatMenu(options));
                    }
                }
            };

            if (!anyAvailable)
            {
                disinformation.Disable("TSA_WD_Dis_DisabledAlert".Translate());
            }

            var primeTarget = targets.FirstOrDefault(t => !t.Comp.IsEspionageOnCooldown) ?? targets.First();
            float baseSuccessChance = seth.TotalDisWeight > 0 ? seth.weightDisSuccess / seth.TotalDisWeight : 0f;
            float skillMod = skill * seth.disSkillSuccessWeightBonus;
            float tierMod = (int)primeTarget.Comp.tier * seth.disTierSuccessWeightPenalty;
            float healthMult = GetPawnHealthFactor(bestDiplomat, seth);
            float totalChance = GetCurrentDisChance(primeTarget.Comp, bestDiplomat, seth);

            disinformation.defaultDesc = "TSA_WD_Dis_GizmoDesc".Translate(
                bestDiplomat.LabelShort,
                skill,
                baseSuccessChance.ToString("P1"),
                skillMod.ToString("P1"),
                tierMod.ToString("P1"),
                healthMult.ToString("P0"),
                totalChance.ToString("P0")
            );

            yield return disinformation;
        }

        private static float GetPawnHealthFactor(Pawn pawn, WorldDominationSettings s)
        {
            float avgCap = (
                pawn.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) +
                pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving) +
                pawn.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation) +
                pawn.health.capacities.GetLevel(PawnCapacityDefOf.Talking) +
                pawn.health.capacities.GetLevel(PawnCapacityDefOf.Sight) +
                pawn.health.capacities.GetLevel(PawnCapacityDefOf.Hearing)
            ) / 6f;

            return Mathf.Lerp(1f, avgCap, s.disHealthImpactWeight);
        }

        private static float GetCurrentDisChance(CompViralSpread comp, Pawn pawn, WorldDominationSettings s)
        {
            float baseSuccessChance = s.TotalDisWeight > 0 ? s.weightDisSuccess / s.TotalDisWeight : 0f;
            float modifiedChance = baseSuccessChance +
                                   pawn.skills.GetSkill(SkillDefOf.Social).Level * s.disSkillSuccessWeightBonus -
                                   (int)comp.tier * s.disTierSuccessWeightPenalty;

            float finalSuccess = modifiedChance * GetPawnHealthFactor(pawn, s);
            return Mathf.Clamp(finalSuccess, 0.01f, 0.99f);
        }

        private static void ExecuteDisinformation(CompViralSpread comp, Caravan caravan, Pawn pawn)
        {
            if (comp.IsEspionageOnCooldown) return;

            var s = WorldDominationMod.settings;
            comp.espionageCooldownUntilTick = Find.TickManager.TicksGame + Mathf.RoundToInt(s.disCooldownDays * 60000f);

            int socialSkill = pawn.skills.GetSkill(SkillDefOf.Social).Level;
            int tier = (int)comp.tier;

            float wSuccess = Mathf.Max(1f, s.weightDisSuccess + socialSkill * s.disSkillSuccessWeightBonus * 100f - tier * s.disTierSuccessWeightPenalty * 100f);
            wSuccess *= GetPawnHealthFactor(pawn, s);

            float totalWeight = wSuccess + s.weightDisCleanFail + s.weightDisInjuredFail + s.weightDisFatalFail;
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
                if (roll < wSuccess + s.weightDisCleanFail) outcome = "CLEAN";
                else if (roll < wSuccess + s.weightDisCleanFail + s.weightDisInjuredFail) outcome = "INJURY";
                else outcome = "FATAL";

                string saveContext = "";

                if (outcome == "INJURY")
                {
                    if (Rand.Value < pawn.skills.GetSkill(SkillDefOf.Social).Level * s.disSocialCleanBonus)
                    {
                        outcome = "CLEAN";
                        saveContext = "SILVERTONGUE";
                    }
                }
                else if (outcome == "FATAL")
                {
                    int combatSkill = Mathf.Max(pawn.skills.GetSkill(SkillDefOf.Shooting).Level, pawn.skills.GetSkill(SkillDefOf.Melee).Level);
                    if (Rand.Value < combatSkill * s.disCombatSurvivalBonus)
                    {
                        outcome = "INJURY";
                        saveContext = "FIGHTBACK";
                    }
                }

                ApplyFailureOutcome(outcome, saveContext, pawn, tier, caravan, settlement, manager);
            }
        }

        private static void HandleSuccess(CompViralSpread comp, Caravan caravan, Pawn pawn, Settlement settlement, WorldComponent_SpreadManager manager, WorldDominationSettings s)
        {
            float damage = s.disBaseReduction + pawn.skills.GetSkill(SkillDefOf.Social).Level * s.disSkillReductionBonus;
            float oldStr = comp.strength;
            comp.strength -= damage;

            if (comp.strength <= 0)
            {
                Find.LetterStack.ReceiveLetter("TSA_WD_Dis_LetterCollapsedLabel".Translate(), "TSA_WD_Dis_LetterCollapsedText".Translate(pawn.LabelShort, settlement.Label), LetterDefOf.PositiveEvent, new GlobalTargetInfo(settlement.Tile));
                manager?.AddLog(new SpreadLogEntry("TSA_WD_Dis_LogDestroyed".Translate(oldStr.ToString("F0")), caravan, settlement));
                Find.WorldObjects.Remove(settlement);
            }
            else
            {
                comp.CheckTierUpdate();
                float newStr = comp.strength;
                Find.LetterStack.ReceiveLetter("TSA_WD_Dis_LetterSuccessLabel".Translate(), "TSA_WD_Dis_LetterSuccessText".Translate(pawn.LabelShort, settlement.Label, damage.ToString("F0")), LetterDefOf.PositiveEvent, settlement);
                manager?.AddLog(new SpreadLogEntry("TSA_WD_Dis_LogSuccess".Translate(oldStr.ToString("F0"), newStr.ToString("F0")), caravan, settlement));
            }
            WorldActions_Utils.RefreshMap();
        }

        private static void ApplyFailureOutcome(string outcome, string saveContext, Pawn pawn, int tier, Caravan caravan, Settlement settlement, WorldComponent_SpreadManager manager)
        {
            string label = "TSA_WD_Dis_LetterFailedLabel".Translate();
            string text = "TSA_WD_Dis_LetterFailedTextBase".Translate(pawn.LabelShort, settlement.Label);
            LetterDef letterDef = LetterDefOf.NeutralEvent;
            string logSuffix = "TSA_WD_Dis_LogClean".Translate();

            if (outcome == "CLEAN")
            {
                text += saveContext == "SILVERTONGUE"
                    ? "TSA_WD_Dis_FailureSilvertongue".Translate()
                    : "TSA_WD_Dis_FailureClean".Translate();
            }
            else
            {
                if (settlement.Faction != null) settlement.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -30);

                if (outcome == "INJURY")
                {
                    letterDef = LetterDefOf.ThreatSmall;
                    logSuffix = "TSA_WD_Dis_LogInjured".Translate();
                    ApplyInjuries(pawn, tier);

                    if (saveContext == "FIGHTBACK")
                        text += "TSA_WD_Dis_FailureFightback".Translate();
                    else
                        text += "TSA_WD_Dis_FailureSpotted".Translate();
                }
                else // FATAL
                {
                    letterDef = LetterDefOf.Death;
                    logSuffix = "TSA_WD_Dis_LogKilled".Translate();
                    pawn.Kill(null);
                    text += "TSA_WD_Dis_FailureExecuted".Translate();
                }
            }

            Find.LetterStack.ReceiveLetter(label, text, letterDef, settlement);
            manager?.AddLog(new SpreadLogEntry("TSA_WD_Dis_LogMain".Translate(logSuffix), caravan, settlement));
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