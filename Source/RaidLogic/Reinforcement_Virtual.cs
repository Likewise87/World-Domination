using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using TSA_WorldDomination.Settings;
using WorldDomination;

namespace TSA_WorldDomination.RaidLogic
{
    public static class Raid_ReinforcementLogic
    {
        // Helper to get the multiplier band based on distance vs max range
        public static float GetDistanceMultiplier(float distance, float maxRange, WorldDominationSettings seth)
        {
            if (maxRange <= 0) return seth.threatDistMult100;
            float pct = distance / maxRange;
            if (pct <= 0.5f) return seth.threatDistMult50;
            if (pct <= 0.75f) return seth.threatDistMult75;
            return seth.threatDistMult100;
        }

        public static List<WorldObject> GetReinforcements(WorldObject primary, WorldObject enemy, float radius, ILookup<Faction, WorldObject> lookup, WorldComponent_SpreadManager manager, List<Faction> excludedFactions = null)
        {
            List<WorldObject> allies = new List<WorldObject>();
            foreach (var f in Find.FactionManager.AllFactionsListForReading)
            {
                if (f == null || f.defeated || f.def.hidden) continue;

                if (excludedFactions != null && excludedFactions.Count > 0)
                {
                    bool isHostileToAllOpponents = true;
                    foreach (var opponentFaction in excludedFactions)
                    {
                        if (f == opponentFaction || !f.HostileTo(opponentFaction))
                        {
                            isHostileToAllOpponents = false;
                            break;
                        }
                    }
                    if (!isHostileToAllOpponents) continue;
                }

                bool isPrimaryFaction = f == primary.Faction;
                bool isValidAllyFaction = false;

                if (!isPrimaryFaction && primary.Faction != null && enemy != null && enemy.Faction != null && f != enemy.Faction)
                {
                    isValidAllyFaction = f.RelationKindWith(primary.Faction) == FactionRelationKind.Ally;
                }

                if (isPrimaryFaction || isValidAllyFaction)
                {
                    foreach (var s in lookup[f])
                    {
                        if (s == primary || s == enemy) continue;
                        if (WorldActions_Utils.IsSpace(s)) continue;

                        if (s.Faction != null && s.Faction.IsPlayer && s is Settlement playerS && playerS.HasMap)
                            continue;

                        if (WorldActions_Utils.GetDistance(primary.Tile, s.Tile, manager) <= radius)
                        {
                            allies.Add(s);
                        }
                    }
                }
            }
            return allies;
        }

        // --- NEW: Simplified Power Calculation (For Attacker "Gather at Hub" Heuristic) ---
        // Applies a flat multiplier (based on the primary attacker's travel) to the entire army.
        public static float CalculateTotalReinforcementPowerSimplified(List<WorldObject> allies, WorldObject primary, float flatMultiplier, out List<string> details)
        {
            details = new List<string>();
            float primaryRaw = primary.GetComponent<CompViralSpread>()?.strength ?? 0f;
            float primaryEff = primaryRaw * flatMultiplier;

            details.Add($"{primary.LabelCap} (Primary): {primaryEff:F0} ({primaryRaw:F0} x {flatMultiplier * 100:F0}%)");

            float totalReinforcementsRaw = 0f;
            foreach (var a in allies)
            {
                float aStr = a.GetComponent<CompViralSpread>()?.strength ?? 0f;
                totalReinforcementsRaw += aStr;

                float aEff = aStr * flatMultiplier;
                details.Add($"{a.LabelCap} (Ally): {aEff:F0} ({aStr:F0} x {flatMultiplier * 100:F0}%)");
            }

            return primaryEff + (totalReinforcementsRaw * flatMultiplier);
        }

        // --- Standard Power Calculation (For Defenders / Localized Response) ---
        public static float CalculateTotalReinforcementPower(List<WorldObject> allies, int centerTile, float maxRadius, WorldDominationSettings seth, WorldComponent_SpreadManager manager, out List<string> details, WorldObject primary, string label)
        {
            details = new List<string>();
            float primaryStr = primary.GetComponent<CompViralSpread>()?.strength ?? 0f;
            details.Add($"{primary.LabelCap} ({label}): Strength {primaryStr:F0}");

            float totalReinforcements = 0f;
            foreach (var a in allies)
            {
                var comp = a.GetComponent<CompViralSpread>();
                if (comp == null) continue;

                float dist = WorldActions_Utils.GetDistance(centerTile, a.Tile, manager);
                float mult = GetDistanceMultiplier(dist, maxRadius, seth);
                float effectiveContrib = comp.strength * mult;
                totalReinforcements += effectiveContrib;

                string distBand = (dist / maxRadius <= 0.5f) ? "0-50%" : (dist / maxRadius <= 0.75f) ? "50-75%" : "75-100%";
                details.Add($"{a.LabelCap}: {effectiveContrib:F0} ({comp.strength:F0} x {mult * 100:F0}%) [Dist: {dist:F0}, Band: {distBand}]");
            }
            return primaryStr + totalReinforcements;
        }

        public static float CalculateTotalReinforcementPower(List<WorldObject> allies, int centerTile, float maxRadius, WorldDominationSettings seth, WorldComponent_SpreadManager manager)
        {
            float total = 0f;
            foreach (var a in allies)
            {
                var comp = a.GetComponent<CompViralSpread>();
                if (comp == null) continue;
                float dist = WorldActions_Utils.GetDistance(centerTile, a.Tile, manager);
                total += comp.strength * GetDistanceMultiplier(dist, maxRadius, seth);
            }
            return total;
        }
    }
}