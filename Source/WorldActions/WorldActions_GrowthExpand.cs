using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using TSA_WorldDomination.Settings;

namespace WorldDomination
{
    public static class WorldActions_GrowthExpand
    {
        public static void AttemptGrow(WorldObject s, CompViralSpread comp, WorldComponent_SpreadManager manager)
        {
            var seth = WorldDominationMod.settings;
            float oldStr = comp.strength;

            // Updated tierMax for T4 support (T4 cap is 2250)
            float tierMax = (comp.tier == SettlementTier.T1) ? 500f :
                            (comp.tier == SettlementTier.T2) ? 1000f :
                            (comp.tier == SettlementTier.T3) ? 1600f : 2250f;

            comp.incidentCooldownUntilTick = Find.TickManager.TicksGame + (int)(seth.cooldownGrowDays * 60000);

            // --- BRANCH A: NORMAL GROWTH ---
            if (oldStr < tierMax)
            {
                float buffMult = (s.Faction == manager.currentWeakestUnderdog && Find.TickManager.TicksGame < manager.underdogBuffExpiryTick) ? 2f : 1f;

                // Scale growth factor based on strength (diminishing returns as they get stronger)
                float rawFactor = (oldStr <= 500f) ? Mathf.Lerp(1.5f, 1.0f, (oldStr - 100f) / 400f) :
                                 (oldStr <= 1000f) ? Mathf.Lerp(1.0f, 0.5f, (oldStr - 500f) / 500f) : 0.3f;

                float finalFactor = (1.0f + (rawFactor - 1.0f) * seth.growthScalingIntensity) * buffMult;

                comp.strength = Mathf.Min(oldStr + (seth.baseGrowthAmount * finalFactor), tierMax);
                manager.AddLog(new SpreadLogEntry("TSA_WD_Log_Grow".Translate(oldStr.ToString("F0"), comp.strength.ToString("F0")), s));
                return;
            }

            // --- BRANCH B: UPGRADE OR EXPAND ---
            // 1. Try Upgrade Logic (T1->T2, T2->T3, T3->T4)
            if (comp.tier != SettlementTier.T4)
            {
                var neighbors = Find.WorldObjects.Settlements.Where(other => other.Faction == s.Faction && other != s &&
                                WorldActions_Utils.IsWdSurfaceWorldObject(other) &&
                                Find.WorldGrid.ApproxDistanceInTiles(s.Tile, other.Tile) <= seth.expandMaxRadius);

                int sameTierNeighbors = 0;
                int t2Count = 0;
                int t3Count = 0;

                foreach (var n in neighbors)
                {
                    var nComp = n.GetComponent<CompViralSpread>();
                    if (nComp == null) continue;
                    if (nComp.tier == comp.tier) sameTierNeighbors++;
                    if (nComp.tier == SettlementTier.T2) t2Count++;
                    if (nComp.tier == SettlementTier.T3) t3Count++;
                }

                // T4 Upgrade requirement: Current T3 needs at least 2 other T3 neighbors
                if (sameTierNeighbors >= 2)
                {
                    // Note: Since we don't have a seth.localMaxT4, we assume only 1 Citadel per region
                    bool canUpgrade = (comp.tier == SettlementTier.T1) ? (t2Count < seth.localMaxT2) :
                                      (comp.tier == SettlementTier.T2) ? (t3Count < seth.localMaxT3) :
                                      (comp.tier == SettlementTier.T3); // T3 to T4 only depends on having 2 T3 neighbors

                    if (canUpgrade)
                    {
                        SettlementTier oldTier = comp.tier;
                        SettlementTier nextTier = (oldTier == SettlementTier.T1) ? SettlementTier.T2 :
                                                 (oldTier == SettlementTier.T2) ? SettlementTier.T3 : SettlementTier.T4;

                        comp.SetState(nextTier);
                        comp.strength += seth.baseGrowthAmount;
                        manager.AddLog(new SpreadLogEntry("TSA_WD_Log_Upgrade".Translate(oldTier.ToString(), comp.tier.ToString(), sameTierNeighbors), s));
                        WorldActions_Utils.RefreshMap();
                        return;
                    }
                }
            }

            // 2. Try Expand
            if (Rand.Value < seth.expansionSuccessChance)
            {
                ExecuteExpansion((Settlement)s, comp, manager);
            }
        }

        private static void ExecuteExpansion(Settlement parent, CompViralSpread parentComp, WorldComponent_SpreadManager manager)
        {
            var seth = WorldDominationMod.settings;

            // Global Cap Check
            int totalGlobal = Find.WorldObjects.Settlements.Count(s => s.Faction != null && !s.Faction.IsPlayer && !s.Faction.def.hidden);
            if (totalGlobal >= seth.maxSettlements) return;

            List<int> validTiles = new List<int>();

            // --- RESTORED WORKING FLOOD FILLER ---
            parent.Tile.Layer.Filler.FloodFill(parent.Tile, (PlanetTile pt) => WorldActions_Utils.IsWdSurfaceTile(pt.tileId), (PlanetTile pt, int dist) =>
            {
                if (dist > seth.expandMaxRadius) return true;
                if (dist >= seth.expandMinRadius && TileFinder.IsValidTileForNewSettlement(pt.tileId) && !Find.WorldObjects.AnyWorldObjectAt(pt.tileId))
                {
                    // Additional check: Target-Based Saturation
                    if (!IsTargetSaturated(pt.tileId, parent.Faction, seth))
                    {
                        validTiles.Add(pt.tileId);
                    }
                }
                return false;
            });

            if (validTiles.TryRandomElement(out int chosenTile))
            {
                float cost = parentComp.strength * 0.25f;
                parentComp.strength -= cost;

                Settlement newS = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                newS.SetFaction(parent.Faction);
                newS.Tile = chosenTile;
                newS.Name = SettlementNameGenerator.GenerateSettlementName(newS);

                var comp = newS.GetComponent<CompViralSpread>();
                if (comp != null)
                {
                    comp.SetState(SettlementTier.T1);
                    comp.strength = (parentComp.tier == SettlementTier.T4) ? 500f :
                    (parentComp.tier == SettlementTier.T3) ? 400f :
                    (parentComp.tier == SettlementTier.T2) ? 250f : 100f;
                }

                Find.WorldObjects.Add(newS);
                manager.AddLog(new SpreadLogEntry("TSA_WD_Log_ExpandSuccess".Translate(parent.Label), parent, newS));
                WorldActions_Utils.RefreshMap();
            }
            else
            {
                manager.AddLog(new SpreadLogEntry("TSA_WD_Log_ExpandSaturated".Translate(), parent));
            }
        }

        private static bool IsTargetSaturated(int tileId, Faction faction, WorldDominationSettings seth)
        {
            // Count nearby T1 settlements of the same faction to determine if this tile is a "cluttered" destination
            int nearbyT1 = 0;
            float checkRadius = seth.expandMaxRadius;

            // Optimized check: only look at settlements
            var settlements = Find.WorldObjects.Settlements;
            for (int i = 0; i < settlements.Count; i++)
            {
                var s = settlements[i];
                if (s.Faction == faction)
                {
                    if (Find.WorldGrid.ApproxDistanceInTiles(tileId, s.Tile) <= checkRadius)
                    {
                        var comp = s.GetComponent<CompViralSpread>();
                        if (comp != null && comp.tier == SettlementTier.T1)
                        {
                            nearbyT1++;
                            if (nearbyT1 >= seth.localMaxT1) return true; // Saturated
                        }
                    }
                }
            }
            return false;
        }
    }
}