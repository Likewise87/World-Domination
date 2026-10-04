using RimWorld.Planet;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using TSA_WorldDomination.Settings;
using Verse;
using WorldDomination;
using UnityEngine;

namespace TSA_WorldDomination.WorldActions
{
    public static class WorldActions_Revolt
    {
        public static void TryTriggerRevolt(WorldComponent_SpreadManager manager)
        {
            var seth = WorldDominationMod.settings;

            var defeatedFactions = Find.FactionManager.AllFactions
                .Where(f => !f.def.hidden
                            && !f.IsPlayer
                            && !WorldActions_Utils.IsExcludedFaction(f)
                            && !Find.WorldObjects.Settlements.Any(s => s.Faction == f && WorldActions_Utils.IsWdSurfaceWorldObject(s)))
                .ToList();

            if (!defeatedFactions.Any()) return;
            if (Rand.Value > (seth.revoltChance / 100f)) return;

            Faction rebelFaction = defeatedFactions.RandomElement();

            Faction targetFaction = Find.FactionManager.AllFactions
                .Where(f => !f.defeated && !f.IsPlayer && !f.def.hidden
                            && !WorldActions_Utils.IsExcludedFaction(f)
                            && Find.WorldObjects.Settlements.Count(s => s.Faction == f && WorldActions_Utils.IsWdSurfaceWorldObject(s)) >= 3)
                .OrderByDescending(f => Find.WorldObjects.Settlements.Count(s => s.Faction == f && WorldActions_Utils.IsWdSurfaceWorldObject(s)))
                .FirstOrDefault();

            if (targetFaction == null) return;

            int numToTake = Rand.RangeInclusive(1, 3);
            var targets = Find.WorldObjects.Settlements
                .Where(s => s.Faction == targetFaction && WorldActions_Utils.IsWdSurfaceWorldObject(s))
                .InRandomOrder()
                .Take(numToTake)
                .ToList();

            if (!targets.Any()) return;

            rebelFaction.defeated = false;
            string oldFactionName = targetFaction.Name;

            List<string> liberatedNames = new List<string>();
            int lastTile = -1;

            foreach (var target in targets)
            {
                int tile = target.Tile;
                var oldComp = target.GetComponent<CompViralSpread>();
                float oldStr = oldComp?.strength ?? 100f;
                SettlementTier oldTier = oldComp?.tier ?? SettlementTier.T1;

                liberatedNames.Add(target.Label);
                lastTile = tile;

                Find.WorldObjects.Remove(target);

                Settlement newS = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                newS.SetFaction(rebelFaction);
                newS.Tile = tile;
                newS.Name = SettlementNameGenerator.GenerateSettlementName(newS);

                var nc = newS.GetComponent<CompViralSpread>();
                if (nc != null)
                {
                    nc.strength = oldStr * 0.8f;
                    nc.CheckTierUpdate();
                }

                Find.WorldObjects.Add(newS);

                // --- LOCALIZED LOG ---
                string tierMsg = (nc != null && nc.tier < oldTier) ? "TSA_WD_Log_Revolt_TierDrop".Translate(nc.tier.ToString()) : "";
                string logText = "TSA_WD_Log_Revolt_Entry".Translate(newS.LabelCap, oldFactionName, oldStr.ToString("F0"), (nc?.strength ?? 0).ToString("F0"), tierMsg);

                manager.AddLog(new SpreadLogEntry(logText, rebelFaction, newS, oldFactionName, targetFaction));
            }

            // --- LOCALIZED GLOBAL LETTER ---
            string settlementList = liberatedNames.ToCommaList(true); // Built-in Verse method for "A, B, and C"
            string letterText = "TSA_WD_Letter_Revolt_Text".Translate(rebelFaction.Name.Colorize(Color.cyan), settlementList, oldFactionName);

            Find.LetterStack.ReceiveLetter("TSA_WD_Letter_Revolt_Label".Translate(), letterText, LetterDefOf.NeutralEvent, new GlobalTargetInfo(lastTile));

            WorldActions_Utils.RefreshMap();
        }
    }
}