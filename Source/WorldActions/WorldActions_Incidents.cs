using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using TSA_WorldDomination.Settings;

namespace WorldDomination
{
    public static class WorldActions_Incidents
    {
        public static void AttemptMinorIncident(Settlement s, CompViralSpread comp, WorldComponent_SpreadManager manager)
        {
            HandleIncident(s, comp, manager, false);
        }

        public static void AttemptMajorIncident(Settlement s, CompViralSpread comp, WorldComponent_SpreadManager manager)
        {
            HandleIncident(s, comp, manager, true);
        }

        private static void HandleIncident(Settlement s, CompViralSpread comp, WorldComponent_SpreadManager manager, bool isMajor)
        {
            var seth = WorldDominationMod.settings;
            float oldStr = comp.strength;
            SettlementTier oldTier = comp.tier;

            // 1. Apply Diplomacy Multipliers
            float mult = 1f;
            if (s.Faction == manager.currentWorldLeader && Find.TickManager.TicksGame < manager.leaderHandicapExpiryTick) mult = 2f;
            if (s.Faction == manager.currentWeakestUnderdog && Find.TickManager.TicksGame < manager.underdogBuffExpiryTick) mult = 0.5f;

            float loss = (isMajor ? seth.majorIncidentSeverity : seth.minorIncidentSeverity) * mult;
            comp.strength -= loss;

            string logKeyPrefix = isMajor ? "TSA_WD_Log_MajorInc" : "TSA_WD_Log_MinorInc";

            // 2. Check for Immediate Destruction
            if (comp.strength < 0)
            {
                manager.AddLog(new SpreadLogEntry($"{logKeyPrefix}_Obliterated".Translate(oldStr.ToString("F0"), comp.strength.ToString("F0")), s));

                if (WorldActions_Utils.IsInPlayerInfluence(s.Tile) && WorldDominationMod.settings.notifySettlementRaided)
                {
                    string letterLabel = (isMajor ? "TSA_WD_Letter_MajorInc_Label" : "TSA_WD_Letter_MinorInc_Label").Translate();
                    string letterText = (isMajor ? "TSA_WD_Letter_MajorInc_Text" : "TSA_WD_Letter_MinorInc_Text").Translate(s.Label);
                    Find.LetterStack.ReceiveLetter(letterLabel, letterText, LetterDefOf.NeutralEvent, new GlobalTargetInfo(s.Tile));
                }

                Find.WorldObjects.Remove(s);
                WorldActions_Utils.RefreshMap();
                return;
            }

            // 3. Centralized Tier Update
            comp.CheckTierUpdate();

            if (comp.tier < oldTier)
            {
                manager.AddLog(new SpreadLogEntry($"{logKeyPrefix}_Downgrade".Translate(loss.ToString("F0"), oldStr.ToString("F0"), comp.strength.ToString("F0"), comp.tier.ToString()), s));
                WorldActions_Utils.RefreshMap();
            }
            else
            {
                manager.AddLog(new SpreadLogEntry($"{logKeyPrefix}_Standard".Translate(loss.ToString("F0"), oldStr.ToString("F0"), comp.strength.ToString("F0")), s));
            }
        }
    }
}