using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using TSA_WorldDomination.Settings;
using Outposts;
using WorldDomination;

namespace TSA_WorldDomination.RaidLogic
{
    public static class WorldActions_Raid
    {
        public static void AttemptRaid(Settlement attacker, CompViralSpread attComp, WorldComponent_SpreadManager manager)
        {
            var seth = WorldDominationMod.settings;
            if (seth == null || attacker.Faction == null) return;

            if (WorldActions_Utils.IsSpace(attacker))
            {
                manager.AddLog(new SpreadLogEntry("RAID: Attacker in space (abort)", attacker));
                return;
            }

            // --- DYNAMIC RANGE CALCULATION ---
            float currentRaidRange = seth.raidTargetRadius;
            if (attacker.Faction == manager.expansionistZealFaction && Find.TickManager.TicksGame < manager.expansionistZealExpiryTick)
            {
                currentRaidRange *= 2f;
            }

            var objectsWithComp = Find.WorldObjects.AllWorldObjects
                .Where(s => s.GetComponent<CompViralSpread>() != null)
                .ToLookup(s => s.Faction);

            // --- BATCHED ATTACKER REINFORCEMENTS ---
            // We gather the attacker's army once. This army "gathers" at the attacker's tile.
            // Note: excludedFactions is null here because we haven't picked a target yet, 
            // but GetReinforcements handles internal hostility correctly.
            var attAllies = Raid_ReinforcementLogic.GetReinforcements(attacker, null, seth.raidAttackerAllyRadius, objectsWithComp, manager);
            float totalRawAttPower = attComp.strength + attAllies.Sum(a => a.GetComponent<CompViralSpread>()?.strength ?? 0f);

            List<WorldObject> potentialTargets = new List<WorldObject>();

            // --- PLAYER TARGET SELECTION ---
            if (seth.allowPlayerRaid && attacker.Faction.HostileTo(Faction.OfPlayer))
            {
                var playerObjects = Find.WorldObjects.AllWorldObjects
                    .Where(obj => obj.Faction != null && obj.Faction.IsPlayer && obj.GetComponent<CompViralSpread>() != null);

                foreach (var po in playerObjects)
                {
                    var pComp = po.GetComponent<CompViralSpread>();
                    if (pComp == null || pComp.IsIncidentOnCooldown) continue;

                    bool isSimulatedTarget = po is Outpost || WorldActions_Utils.IsVassal(po);
                    if (isSimulatedTarget && !seth.allowPlayerOutpostRaid) continue;

                    float dist = WorldActions_Utils.GetDistance(attacker.Tile, po.Tile, manager);
                    if (dist <= currentRaidRange)
                    {
                        // APPLY HEURISTIC DISCOUNT: Entire army power is discounted based on Attacker -> Player Object distance
                        float attMult = Raid_ReinforcementLogic.GetDistanceMultiplier(dist, currentRaidRange, seth);
                        float effectiveAttPower = totalRawAttPower * attMult;

                        if (po is Settlement s && s.HasMap)
                        {
                            potentialTargets.Add(po);
                        }
                        else if (isSimulatedTarget)
                        {
                            // Heuristic comparison vs Player strength (Player colonies don't get reinforcements in this mod logic)
                            if (effectiveAttPower >= pComp.strength * seth.minRaidRatio)
                            {
                                potentialTargets.Add(po);
                            }
                        }
                    }
                }
            }

            // --- NPC TARGET SELECTION ---
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || f == attacker.Faction || !f.HostileTo(attacker.Faction) || f.IsPlayer) continue;

                foreach (var obj in objectsWithComp[f])
                {
                    if (WorldActions_Utils.IsSpace(obj)) continue;
                    if (obj is Settlement s && WorldActions_Utils.IsSettlementProtected(s)) continue;

                    float dist = WorldActions_Utils.GetDistance(attacker.Tile, obj.Tile, manager);
                    if (dist <= currentRaidRange)
                    {
                        // APPLY HEURISTIC DISCOUNT: Attacker army travels from 'attacker' to 'obj'
                        float attMult = Raid_ReinforcementLogic.GetDistanceMultiplier(dist, currentRaidRange, seth);
                        float effectiveAttPower = totalRawAttPower * attMult;

                        // Expensive lookup for Defender allies is only done IF Attacker army has a chance
                        // We use a slight padding (0.7f) so we don't prune targets that reinforcements might balance out
                        if (effectiveAttPower >= (obj.GetComponent<CompViralSpread>()?.strength ?? 0f) * (seth.minRaidRatio * 0.7f))
                        {
                            var defAllies = Raid_ReinforcementLogic.GetReinforcements(obj, attacker, seth.raidDefenderAllyRadius, objectsWithComp, manager);
                            float totalDefPower = (obj.GetComponent<CompViralSpread>()?.strength ?? 0f) +
                                                Raid_ReinforcementLogic.CalculateTotalReinforcementPower(defAllies, obj.Tile, seth.raidDefenderAllyRadius, seth, manager);

                            if (effectiveAttPower >= totalDefPower * seth.minRaidRatio)
                            {
                                potentialTargets.Add(obj);
                            }
                        }
                    }
                }
            }

            WorldObject target = potentialTargets.RandomElementWithFallback();
            if (target == null)
            {
                // ABORT LOGIC: Finding the best "almost" target for the log
                WorldObject anyTargetInRange = Find.WorldObjects.Settlements.FirstOrDefault(s =>
                    s.Faction != null && s.Faction.HostileTo(attacker.Faction) && s != attacker &&
                    WorldActions_Utils.GetDistance(attacker.Tile, s.Tile, manager) <= currentRaidRange);

                if (anyTargetInRange != null)
                {
                    float dist = WorldActions_Utils.GetDistance(attacker.Tile, anyTargetInRange.Tile, manager);
                    float attMult = Raid_ReinforcementLogic.GetDistanceMultiplier(dist, currentRaidRange, seth);

                    var defAllies = Raid_ReinforcementLogic.GetReinforcements(anyTargetInRange, attacker, seth.raidDefenderAllyRadius, objectsWithComp, manager);

                    // Note: In abort logs, we use the detailed version of power calculation for clarity
                    List<string> abortAttDetails;
                    float finalAttPower = Raid_ReinforcementLogic.CalculateTotalReinforcementPowerSimplified(attAllies, attacker, attMult, out abortAttDetails);

                    List<string> abortDefDetails;
                    float finalDefPower = (anyTargetInRange.GetComponent<CompViralSpread>()?.strength ?? 0f) +
                                         Raid_ReinforcementLogic.CalculateTotalReinforcementPower(defAllies, anyTargetInRange.Tile, seth.raidDefenderAllyRadius, seth, manager, out abortDefDetails, anyTargetInRange, "Defender");

                    manager.AddLog(new SpreadLogEntry("RAID: Aborted (Targets too strong with allies/distance)", attacker, anyTargetInRange)
                    {
                        isRaid = true,
                        attStr = finalAttPower,
                        defStr = finalDefPower,
                        ratio = finalAttPower / (finalDefPower > 0 ? finalDefPower : 1f),
                        attDetails = abortAttDetails,
                        defDetails = abortDefDetails
                    });
                }
                return;
            }

            // --- FINAL RAID EXECUTION ---
            float finalDist = WorldActions_Utils.GetDistance(attacker.Tile, target.Tile, manager);
            float finalAttMult = Raid_ReinforcementLogic.GetDistanceMultiplier(finalDist, currentRaidRange, seth);

            var finalDefAllies = Raid_ReinforcementLogic.GetReinforcements(target, attacker, seth.raidDefenderAllyRadius, objectsWithComp, manager);

            List<string> finalAttDetails;
            float finalAttAgg = Raid_ReinforcementLogic.CalculateTotalReinforcementPowerSimplified(attAllies, attacker, finalAttMult, out finalAttDetails);

            attComp.incidentCooldownUntilTick = Find.TickManager.TicksGame + Mathf.RoundToInt(seth.cooldownRaidDays * 60000f);

            List<WorldObject> fullAttList = new List<WorldObject> { attacker };
            fullAttList.AddRange(attAllies);

            if (target.Faction != null && target.Faction.IsPlayer && target is Settlement playerSettlement && playerSettlement.HasMap)
            {
                Raid_OnPlayerColony.HandleRaidOnPlayer(attacker, playerSettlement, fullAttList, finalAttAgg, finalAttDetails, manager);
            }
            else
            {
                Raid_Simulated.HandleSimulationRaid(attacker, target, fullAttList, finalAttAgg, finalAttDetails, finalDefAllies, manager);
            }
        }
    }
}