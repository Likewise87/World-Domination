using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using TSA_WorldDomination.Settings;
using Outposts;
using WorldDomination;

namespace TSA_WorldDomination.RaidLogic
{
    public static class Raid_Simulated
    {
        public static void HandleSimulationRaid(Settlement attacker, WorldObject target, List<WorldObject> attList, float attAgg, List<string> attDetails, List<WorldObject> defAllies, WorldComponent_SpreadManager manager)
        {
            var seth = WorldDominationMod.settings;
            var targetComp = target.GetComponent<CompViralSpread>();
            float defAgg = targetComp?.strength ?? 100f;

            // --- DEFENDER REINFORCEMENTS (Localized Response) ---
            // Defenders still use individual distance math as they are rushing to the target location.
            List<string> defDetails = new List<string> { "TSA_WD_Log_Raid_DefBase".Translate(target.LabelCap, defAgg.ToString("F0")) };

            foreach (var wo in defAllies)
            {
                var c = wo.GetComponent<CompViralSpread>();
                if (c == null) continue;

                float dist = WorldActions_Utils.GetDistance(target.Tile, wo.Tile, manager);
                // Standardizing using the new helper method
                float mult = Raid_ReinforcementLogic.GetDistanceMultiplier(dist, seth.raidDefenderAllyRadius, seth);

                float distPct = dist / seth.raidDefenderAllyRadius;
                string distBand = distPct <= 0.5f ? "0-50%" : distPct <= 0.75f ? "50-75%" : "75-100%";
                float effectiveContrib = c.strength * mult;
                defAgg += effectiveContrib;

                string facType = (wo.Faction == target.Faction) ? "TSA_WD_SameFaction".Translate() : "TSA_WD_AlliedFaction".Translate();
                defDetails.Add("TSA_WD_Log_Raid_DefContrib".Translate(wo.LabelCap, facType, effectiveContrib.ToString("F0"), c.strength.ToString("F0"), (mult * 100).ToString("F0"), dist.ToString("F0"), distBand));
            }

            List<WorldObject> fullDefList = new List<WorldObject> { target };
            fullDefList.AddRange(defAllies);

            // --- RATIO & OUTCOME CALCULATION ---
            float ratio = attAgg / (defAgg > 0 ? defAgg : 1f);

            // Note: Potential targets were already filtered by minRaidRatio in AttemptRaid, 
            // but we keep this check for secondary validation.
            if (ratio < seth.minRaidRatio)
            {
                manager.AddLog(new SpreadLogEntry("TSA_WD_Log_Raid_Aborted".Translate(), attacker, target)
                {
                    isRaid = true,
                    attStr = attAgg,
                    defStr = defAgg,
                    ratio = ratio,
                    attDetails = attDetails,
                    defDetails = defDetails
                });
                return;
            }

            // --- START LINEAR INTERPOLATION LOGIC ---
            float winChance, attLossWin, attLossLoss, defLossWin, defLossLoss;
            var outcomes = seth.raidOutcomes.OrderBy(o => o.threshold).ToList();

            if (outcomes.Count == 0)
            {
                winChance = 0f; attLossWin = 0.2f; attLossLoss = 0.5f; defLossWin = 0.2f; defLossLoss = 1.0f;
            }
            else if (ratio <= outcomes.First().threshold)
            {
                var o = outcomes.First();
                winChance = o.winChance; attLossWin = o.attLossWin; attLossLoss = o.attLossLoss; defLossWin = o.defLossWin; defLossLoss = o.defLossLoss;
            }
            else if (ratio >= outcomes.Last().threshold)
            {
                var o = outcomes.Last();
                winChance = o.winChance; attLossWin = o.attLossWin; attLossLoss = o.attLossLoss; defLossWin = o.defLossWin; defLossLoss = o.defLossLoss;
            }
            else
            {
                winChance = 0f; attLossWin = 0f; attLossLoss = 0f; defLossWin = 0f; defLossLoss = 0f;
                for (int i = 0; i < outcomes.Count - 1; i++)
                {
                    var lower = outcomes[i];
                    var upper = outcomes[i + 1];
                    if (ratio >= lower.threshold && ratio <= upper.threshold)
                    {
                        float t = (ratio - lower.threshold) / (upper.threshold - lower.threshold);
                        winChance = Mathf.Lerp(lower.winChance, upper.winChance, t);
                        attLossWin = Mathf.Lerp(lower.attLossWin, upper.attLossWin, t);
                        attLossLoss = Mathf.Lerp(lower.attLossLoss, upper.attLossLoss, t);
                        defLossWin = Mathf.Lerp(lower.defLossWin, upper.defLossWin, t);
                        defLossLoss = Mathf.Lerp(lower.defLossLoss, upper.defLossLoss, t);
                        break;
                    }
                }
            }

            int defendTicks = Mathf.RoundToInt(seth.cooldownBeingRaidedDays * 60000f);
            foreach (var wo in fullDefList)
            {
                var dComp = wo.GetComponent<CompViralSpread>();
                if (dComp != null) dComp.incidentCooldownUntilTick = Find.TickManager.TicksGame + defendTicks;
            }

            bool won = Rand.Value < winChance;
            float attLossPct = won ? attLossWin : attLossLoss;
            float defLossPct = won ? defLossLoss : defLossWin;

            Dictionary<WorldObject, float> attStrengthsBefore = attList.ToDictionary(wo => wo, wo => wo.GetComponent<CompViralSpread>()?.strength ?? 0f);
            Dictionary<WorldObject, float> defStrengthsBefore = fullDefList.ToDictionary(wo => wo, wo => wo.GetComponent<CompViralSpread>()?.strength ?? 0f);

            foreach (var wo in attList) wo.GetComponent<CompViralSpread>()?.ReduceStrength(attLossPct);
            foreach (var wo in fullDefList)
            {
                if (won && wo == target) continue;
                wo.GetComponent<CompViralSpread>()?.ReduceStrength(defLossPct);
            }

            // --- NOTIFICATIONS ---
            if (target.Faction != null && target.Faction.IsPlayer && (target is Outpost || WorldActions_Utils.IsVassal(target)))
            {
                if (WorldDominationMod.settings.notifyOutpostDestroyed)
                {
                    string label = won ? "TSA_WD_Letter_OutpostRaided_Label".Translate() : "TSA_WD_Letter_OutpostDefended_Label".Translate();
                    string text = won ? "TSA_WD_Letter_OutpostRaided_Text".Translate(target.LabelCap, attacker.Faction.Name)
                                      : "TSA_WD_Letter_OutpostDefended_Text".Translate(target.LabelCap, attacker.Faction.Name);
                    Find.LetterStack.ReceiveLetter(label, text, won ? LetterDefOf.NegativeEvent : LetterDefOf.PositiveEvent, target);
                }
            }

            // Prepare Final Logs
            List<string> finalAttDetails = new List<string>();
            for (int i = 0; i < attList.Count; i++)
            {
                var wo = attList[i];
                float before = attStrengthsBefore[wo];
                float after = wo.GetComponent<CompViralSpread>()?.strength ?? 0f;
                finalAttDetails.Add((wo == attacker) ? "TSA_WD_Log_Raid_AttBase".Translate(wo.LabelCap, before.ToString("F0"), after.ToString("F0")) : $"{attDetails[i]} -> {after:F0}");
            }

            List<string> finalDefDetails = new List<string>();
            for (int i = 0; i < fullDefList.Count; i++)
            {
                var wo = fullDefList[i];
                float before = defStrengthsBefore[wo];
                float after = (won && wo == target) ? 0 : wo.GetComponent<CompViralSpread>()?.strength ?? 0f;
                finalDefDetails.Add((wo == target) ? "TSA_WD_Log_Raid_DefBaseFinal".Translate(wo.LabelCap, before.ToString("F0"), after.ToString("F0")) : $"{defDetails[i]} -> {after:F0}");
            }

            if (won)
            {
                string oldLabel = $"{target.LabelCap} ({target.Faction?.Name ?? "???"})";
                string originalName = (target is Settlement sOld) ? sOld.Name : target.Label;
                int tile = target.Tile;
                var oldComp = target.GetComponent<CompViralSpread>();
                SettlementTier tier = oldComp?.tier ?? SettlementTier.T1;

                Find.WorldObjects.Remove(target);

                if (target is Outpost || WorldActions_Utils.IsVassal(target))
                {
                    if (Rand.Value < 0.50f)
                    {
                        Settlement newS = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                        newS.SetFaction(attacker.Faction);
                        newS.Tile = tile;
                        newS.Name = originalName;
                        var nc = newS.GetComponent<CompViralSpread>();
                        if (nc != null) { nc.SetState(SettlementTier.T1); nc.strength = 100f; }
                        Find.WorldObjects.Add(newS);
                        ApplyLossesAndLog(attacker, target, attList, fullDefList, attStrengthsBefore, defStrengthsBefore, attLossPct, defLossPct, "TSA_WD_Log_Raid_OutpostOccupied".Translate(), oldLabel, manager, attAgg, defAgg, ratio, true, winChance, finalAttDetails, finalDefDetails);
                    }
                    else
                    {
                        ApplyLossesAndLog(attacker, target, attList, fullDefList, attStrengthsBefore, defStrengthsBefore, attLossPct, defLossPct, "TSA_WD_Log_Raid_OutpostWiped".Translate(), oldLabel + " [DESTROYED]", manager, attAgg, defAgg, ratio, true, winChance, finalAttDetails, finalDefDetails);
                    }
                }
                else
                {
                    bool razed = Rand.Value < seth.razeChance;
                    if (razed)
                    {
                        ApplyLossesAndLog(attacker, target, attList, fullDefList, attStrengthsBefore, defStrengthsBefore, attLossPct, defLossPct, "TSA_WD_Log_Raid_Razed".Translate(), oldLabel + " [RAZED]", manager, attAgg, defAgg, ratio, true, winChance, finalAttDetails, finalDefDetails);
                    }
                    else
                    {
                        Settlement newS = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                        newS.SetFaction(attacker.Faction);
                        newS.Tile = tile;
                        newS.Name = originalName;
                        var nc = newS.GetComponent<CompViralSpread>();
                        if (nc != null) { nc.tier = tier; nc.strength = CompViralSpread.GetStrengthRange(tier).min; nc.CheckTierUpdate(); }
                        Find.WorldObjects.Add(newS);
                        ApplyLossesAndLog(attacker, target, attList, fullDefList, attStrengthsBefore, defStrengthsBefore, attLossPct, defLossPct, "TSA_WD_Log_Raid_Successful".Translate(), oldLabel, manager, attAgg, defAgg, ratio, true, winChance, finalAttDetails, finalDefDetails);
                    }
                }
            }
            else
            {
                ApplyLossesAndLog(attacker, target, attList, fullDefList, attStrengthsBefore, defStrengthsBefore, attLossPct, defLossPct, "TSA_WD_Log_Raid_Failed".Translate(), null, manager, attAgg, defAgg, ratio, false, winChance, finalAttDetails, finalDefDetails);
            }
        }

        private static void ApplyLossesAndLog(Settlement attacker, WorldObject target, List<WorldObject> attList, List<WorldObject> defList, Dictionary<WorldObject, float> attBefore, Dictionary<WorldObject, float> defBefore, float attLossPct, float defLossPct, string message, string customOldLabel, WorldComponent_SpreadManager manager, float attAgg, float defAgg, float ratio, bool victory, float winChance, List<string> finalAttDetails, List<string> finalDefDetails)
        {
            SpreadLogEntry entry = new SpreadLogEntry(message, attacker, target);
            if (customOldLabel != null) entry.labelB = customOldLabel;
            entry.isRaid = true;
            entry.attStr = attAgg;
            entry.defStr = defAgg;
            entry.ratio = ratio;
            entry.victory = victory;
            entry.winChance = winChance;
            entry.attLossPct = attLossPct;
            entry.defLossPct = defLossPct;
            entry.attDetails = finalAttDetails;
            entry.defDetails = finalDefDetails;
            manager.AddLog(entry);

            if (victory && target != null && target.Faction != null && !target.Faction.IsPlayer)
            {
                if (WorldActions_Utils.IsInPlayerInfluence(target.Tile))
                {
                    if (WorldDominationMod.settings.notifySettlementRaided)
                    {
                        Find.LetterStack.ReceiveLetter("TSA_WD_Letter_NearbyRaid_Label".Translate(),
                                    "TSA_WD_Letter_NearbyRaid_Text".Translate(target.LabelCap, target.Faction.Name, attacker.Faction.Name),
                                    LetterDefOf.NeutralEvent, new GlobalTargetInfo(target.Tile));
                    }
                }
            }
        }
    }
}