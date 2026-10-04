using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using TSA_WorldDomination.Settings;
using WorldDomination;

namespace TSA_WorldDomination.RaidLogic
{
    public static class Raid_OnPlayerColony
    {
        public static void HandleRaidOnPlayer(Settlement attacker, Settlement target, List<WorldObject> attList, float attAgg, List<string> attDetails, WorldComponent_SpreadManager manager)
        {
            var seth = WorldDominationMod.settings;
            Map map = target.Map;
            if (map == null) return;

            float basePoints = StorytellerUtility.DefaultThreatPointsNow(map);

            var targetComp = target.GetComponent<CompViralSpread>();
            if (targetComp != null)
                targetComp.incidentCooldownUntilTick = Find.TickManager.TicksGame + Mathf.RoundToInt(seth.cooldownPlayerRaidDays * 60000f);

            IncidentParms parms = new IncidentParms();
            parms.target = map;
            parms.faction = attacker.Faction;
            parms.forced = true;
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            parms.points = basePoints;

            // --- LOCALIZED LETTER ---
            parms.customLetterLabel = "TSA_WD_Letter_RaidPlayer_Label".Translate(attacker.Faction.Name);
            parms.customLetterText = "TSA_WD_Letter_RaidPlayer_Text".Translate(attacker.Label);

            if (IncidentDefOf.RaidEnemy.Worker.TryExecute(parms))
            {
                float costPct = seth.raidCostPct;
                List<string> finalAttDetails = new List<string>();
                for (int i = 0; i < attList.Count; i++)
                {
                    var wo = attList[i];
                    var c = wo.GetComponent<CompViralSpread>();
                    if (c != null)
                    {
                        float sBefore = c.strength;
                        c.ReduceStrength(costPct);
                        float sAfter = c.strength;

                        if (wo == attacker)
                            finalAttDetails.Add("TSA_WD_Log_Raid_AttBase".Translate(wo.LabelCap, sBefore.ToString("F0"), sAfter.ToString("F0")));
                        else
                            finalAttDetails.Add($"{attDetails[i]} -> {sAfter.ToString("F0")}");
                    }
                }

                // --- LOCALIZED LOG ---
                SpreadLogEntry entry = new SpreadLogEntry("TSA_WD_Log_Raid_PlayerAttack".Translate(), attacker, target);
                entry.isRaid = true;
                entry.victory = true; // Triggered successfully
                entry.attStr = attAgg;
                entry.defStr = basePoints;
                entry.attLossPct = costPct;
                entry.attDetails = finalAttDetails;
                entry.defDetails = new List<string> { "TSA_WD_Log_Raid_PlayerDefDetail".Translate(target.LabelCap, basePoints.ToString("F0")) };

                manager.AddLog(entry);
            }
        }
    }
}