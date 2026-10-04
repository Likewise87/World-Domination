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

namespace TSA_WorldDomination.WorldActions
{
    public static class WorldActions_DiplomacyBuffsNerfs
    {
        private const int GameLaunchGracePeriodTicks = 600; //60000

        private static bool IsGracePeriodActive => Find.TickManager.TicksGame < GameLaunchGracePeriodTicks;

        // 1. WORLD LEADER DEBUFF
        public static void ApplyLeaderHandicap(WorldComponent_SpreadManager manager)
        {
            if (IsGracePeriodActive) return;
            var seth = WorldDominationMod.settings;
            if (!seth.enableLeaderHandicap) return;

            // CHECK: Active status or Cooldown
            if (manager.currentWorldLeader != null && Find.TickManager.TicksGame < manager.leaderHandicapExpiryTick) return;
            if (Find.TickManager.TicksGame < manager.leaderHandicapCooldownTick) return;

            var stats = WorldActions_Utils.GetWorldPowerStats();
            var npcStats = stats.FactionStats.Where(fs => fs.faction != Faction.OfPlayer).ToList();

            if (npcStats.Count < 2) return;

            var leaderStat = npcStats[0];
            var runnerUp = npcStats[1];

            float worldAvg = stats.GlobalTotalStr / stats.FactionStats.Count;
            float leadRatio = leaderStat.TotalStr / runnerUp.TotalStr;

            bool isDominant = leaderStat.TotalStr > worldAvg * 1.3f && leadRatio > 1.1f;
            if (!isDominant) return;

            Faction leader = leaderStat.faction;
            manager.currentWorldLeader = leader;

            int durationTicks = Mathf.RoundToInt(seth.durLeaderHandicapDays * 60000f);
            int cooldownTicks = Mathf.RoundToInt(seth.cdLeaderHandicapDays * 60000f);

            manager.leaderHandicapExpiryTick = Find.TickManager.TicksGame + durationTicks;
            manager.leaderHandicapCooldownTick = Find.TickManager.TicksGame + durationTicks + cooldownTicks;

            string msg = "TSA_WD_Diplo_LeaderHandicap_Msg".Translate(leader.Name.Colorize(Color.cyan), seth.durLeaderHandicapDays.ToString("F0"));
            var anchorSettlement = Find.WorldObjects.Settlements.Where(s => s.Faction == leader).RandomElementWithFallback();
            manager.AddLog(new SpreadLogEntry(msg, anchorSettlement, null));

            if (seth.notifyBuffNerf)
            {
                Find.LetterStack.ReceiveLetter("TSA_WD_Diplo_LeaderHandicap_Label".Translate(), msg, LetterDefOf.NeutralEvent);
            }
        }

        // 2. WEAKEST FACTION BUFF
        public static void ApplyUnderdogBuff(WorldComponent_SpreadManager manager)
        {
            if (IsGracePeriodActive) return;
            var seth = WorldDominationMod.settings;
            if (!seth.enableUnderdogBuff) return;

            // CHECK: Active status or Cooldown
            if (manager.currentWeakestUnderdog != null && Find.TickManager.TicksGame < manager.underdogBuffExpiryTick) return;
            if (Find.TickManager.TicksGame < manager.underdogBuffCooldownTick) return;

            var stats = WorldActions_Utils.GetWorldPowerStats();
            var npcStats = stats.FactionStats.Where(fs => fs.faction != Faction.OfPlayer).ToList();

            if (npcStats.Count < 2) return;

            var weaklingStat = npcStats.LastOrDefault();
            if (weaklingStat == null) return;

            float worldAvg = stats.GlobalTotalStr / stats.FactionStats.Count;

            bool isPathetic = weaklingStat.TotalStr < worldAvg * 0.8f;
            if (!isPathetic) return;

            Faction underdog = weaklingStat.faction;
            manager.currentWeakestUnderdog = underdog;

            int durationTicks = Mathf.RoundToInt(seth.durUnderdogBuffDays * 60000f);
            int cooldownTicks = Mathf.RoundToInt(seth.cdUnderdogBuffDays * 60000f);

            manager.underdogBuffExpiryTick = Find.TickManager.TicksGame + durationTicks;
            manager.underdogBuffCooldownTick = Find.TickManager.TicksGame + durationTicks + cooldownTicks;

            string msg = "TSA_WD_Diplo_UnderdogBuff_Msg".Translate(underdog.Name.Colorize(Color.cyan), seth.durUnderdogBuffDays.ToString("F0"));
            var anchorSettlement = Find.WorldObjects.Settlements.Where(s => s.Faction == underdog).RandomElementWithFallback();
            manager.AddLog(new SpreadLogEntry(msg, anchorSettlement, null));

            if (seth.notifyBuffNerf)
            {
                Find.LetterStack.ReceiveLetter("TSA_WD_Diplo_UnderdogBuff_Label".Translate(), msg, LetterDefOf.NeutralEvent);
            }
        }

        // 3. COALITION AGAINST LEADER
        public static void FormAntiLeaderCoalition(WorldComponent_SpreadManager manager)
        {
            if (IsGracePeriodActive) return;
            var seth = WorldDominationMod.settings;
            if (!seth.enableAntiLeaderCoalition) return;

            if (Find.TickManager.TicksGame < manager.antiLeaderCoalitionCooldownTick) return;

            var stats = WorldActions_Utils.GetWorldPowerStats();
            var npcStats = stats.FactionStats.Where(fs => fs.faction != Faction.OfPlayer).ToList();

            // Need at least 4 NPCs to justify a coalition that skips the #2 runner-up
            if (npcStats.Count < 4) return;

            var leaderStat = npcStats[0];
            var runnerUp = npcStats[1];

            float worldAvg = stats.GlobalTotalStr / stats.FactionStats.Count;
            float leadRatio = leaderStat.TotalStr / runnerUp.TotalStr;

            // Is the leader dominant?
            bool isDominant = leaderStat.TotalStr > worldAvg * 1.4f && leadRatio > 1.1f;
            if (!isDominant) return;

            Faction leader = leaderStat.faction;

            // 1. SCANNING LOGIC: Identify potential members from weakest up to #3
            // We skip [0] (Leader) and [1] (Runner-Up)
            var candidates = npcStats
                .Skip(2)
                .Select(x => x.faction)
                .Reverse() // Start from the absolute weakest
                .ToList();

            List<Faction> coalitionMembers = new List<Faction>();
            float combinedCoalitionStr = 0f;
            int targetMemberCount = 3;

            foreach (var candidate in candidates)
            {
                if (coalitionMembers.Count >= targetMemberCount) break;

                // Check Matrix Lock & Freeze with Leader
                if (seth.IsPairLocked(candidate, leader) || IsFrozen(candidate, leader, manager)) continue;

                // Check Compatibility with existing group members
                bool compatible = true;
                foreach (var member in coalitionMembers)
                {
                    if (seth.IsPairLocked(candidate, member) || IsFrozen(candidate, member, manager))
                    {
                        compatible = false;
                        break;
                    }
                }

                if (compatible)
                {
                    coalitionMembers.Add(candidate);
                    // Track strength for the power check
                    combinedCoalitionStr += npcStats.First(s => s.faction == candidate).TotalStr;
                }
            }

            // 2. STRENGTH CHECK: Does the coalition actually pose a threat?
            // They should represent at least 50% of the leader's strength combined to bother declaring war.
            if (coalitionMembers.Count < 2 || combinedCoalitionStr < (leaderStat.TotalStr * 0.5f))
            {
                if (Prefs.DevMode && coalitionMembers.Count >= 2)
                    Log.Message($"[WorldDomination] Coalition formed but too weak ({combinedCoalitionStr} vs {leaderStat.TotalStr}). Skipping.");
                return;
            }

            // 3. EXECUTION
            int durationTicks = Mathf.RoundToInt(seth.durAntiLeaderCoalitionDays * 60000f);
            int freezeExpiry = Find.TickManager.TicksGame + durationTicks;
            manager.antiLeaderCoalitionCooldownTick = freezeExpiry + Mathf.RoundToInt(seth.cdAntiLeaderCoalitionDays * 60000f);

            for (int i = 0; i < coalitionMembers.Count; i++)
            {
                for (int j = i + 1; j < coalitionMembers.Count; j++)
                {
                    ForceDiplomacy(coalitionMembers[i], coalitionMembers[j], FactionRelationKind.Ally, manager, freezeExpiry);
                }
                ForceDiplomacy(coalitionMembers[i], leader, FactionRelationKind.Hostile, manager, freezeExpiry);
            }

            // Final Notification
            string memberNames = coalitionMembers.Select(m => m.Name.Colorize(Color.cyan)).ToCommaList(true);
            string msg = "TSA_WD_Diplo_Coalition_Msg".Translate(memberNames, leader.Name.Colorize(Color.red));
            var leaderSettlement = Find.WorldObjects.Settlements.Where(s => s.Faction == leader).RandomElementWithFallback();
            manager.AddLog(new SpreadLogEntry(msg, leaderSettlement, null));

            if (seth.notifyDiplomaticChange)
            {
                Find.LetterStack.ReceiveLetter("TSA_WD_Diplo_Coalition_Label".Translate(), msg, LetterDefOf.NeutralEvent);
            }
        }

        // Add this helper if not already present
        private static bool IsFrozen(Faction a, Faction b, WorldComponent_SpreadManager manager)
        {
            return manager.diplomacyFreezeTicks.TryGetValue(GetPairKey(a, b), out int expiry) && Find.TickManager.TicksGame < expiry;
        }


        // 4. EXPANSIONIST ZEAL BUFF (Double Raid Range)
        public static void ApplyExpansionistZeal(WorldComponent_SpreadManager manager)
        {
            if (IsGracePeriodActive) return;
            var seth = WorldDominationMod.settings;
            if (!seth.enableExpansionistZeal) return;

            // CHECK: Active status or Cooldown
            if (manager.expansionistZealFaction != null && Find.TickManager.TicksGame < manager.expansionistZealExpiryTick) return;
            if (Find.TickManager.TicksGame < manager.expansionistZealCooldownTick) return;

            // CHANCE: Random likelihood check (e.g., 5% daily chance)
            if (Rand.Value > seth.zealTriggerChance) return;

            var factions = Find.FactionManager.AllFactionsVisible
                .Where(f => !f.IsPlayer && !f.defeated && !WorldActions_Utils.IsExcludedFaction(f)).ToList();

            if (factions.Count == 0) return;

            Faction zealot = factions.RandomElement();
            manager.expansionistZealFaction = zealot;

            int durationTicks = Mathf.RoundToInt(seth.durExpansionistZealDays * 60000f);
            int cooldownTicks = Mathf.RoundToInt(seth.cdExpansionistZealDays * 60000f);

            manager.expansionistZealExpiryTick = Find.TickManager.TicksGame + durationTicks;
            manager.expansionistZealCooldownTick = Find.TickManager.TicksGame + durationTicks + cooldownTicks;

            string zealotName = zealot.Name.Colorize(Color.cyan);
            string label = "TSA_WD_Diplo_Zeal_Label".Translate(zealotName);
            string msg = "TSA_WD_Diplo_ExpansionistZeal_Msg".Translate(zealotName, seth.durExpansionistZealDays.ToString("F0"));

            var anchor = Find.WorldObjects.Settlements.Where(s => s.Faction == zealot).RandomElementWithFallback();
            manager.AddLog(new SpreadLogEntry(msg, anchor, null));

            if (seth.notifyBuffNerf)
            {
                LetterDef letterDef = zealot.HostileTo(Faction.OfPlayer) ? LetterDefOf.NegativeEvent : LetterDefOf.NeutralEvent;
                Find.LetterStack.ReceiveLetter(label, msg, letterDef, new GlobalTargetInfo(anchor));
            }
        }
        private static void ForceDiplomacy(Faction facA, Faction facB, FactionRelationKind next, WorldComponent_SpreadManager manager, int expiry)
        {
            int targetGoodwill = next == FactionRelationKind.Hostile ? -80 : next == FactionRelationKind.Ally ? 80 : 0;

            FactionRelation relA = facA.RelationWith(facB, false);
            relA.baseGoodwill = targetGoodwill;
            relA.kind = next;

            FactionRelation relB = facB.RelationWith(facA, false);
            relB.baseGoodwill = targetGoodwill;
            relB.kind = next;

            bool flag;
            relA.CheckKindThresholds(facA, false, null, GlobalTargetInfo.Invalid, out flag);
            relB.CheckKindThresholds(facB, false, null, GlobalTargetInfo.Invalid, out flag);

            long key = GetPairKey(facA, facB);
            manager.diplomacyFreezeTicks[key] = expiry;
        }

        public static void TryChangeAllegiances(WorldComponent_SpreadManager manager)
        {
            if (IsGracePeriodActive) return;
            if (!WorldDominationMod.settings.enableRandomDiplomacy) return;
            if (Current.ProgramState != ProgramState.Playing || Faction.OfPlayer == null) return;

            var seth = WorldDominationMod.settings;
            if (Rand.Value > seth.diplomacyChangeChance) return;

            var validFactions = Find.FactionManager.AllFactionsVisible
                .Where(f => f != null && !f.IsPlayer && !WorldActions_Utils.IsExcludedFaction(f))
                .ToList();

            if (validFactions.Count < 2) return;

            Faction facA = validFactions.RandomElement();
            Faction facB = validFactions.Where(f => f != facA).RandomElementWithFallback();
            if (facA == null || facB == null) return;

            // --- NEW LOGGING: Check for Lock ---
            if (seth.IsPairLocked(facA, facB))
            {
                // We log to the dev console so you can see it working, 
                // but NOT to the in-game action log (to keep it clean for the player)
                if (Prefs.DevMode)
                    Log.Message($"[WorldDomination] Diplomacy: Change between {facA.Name} and {facB.Name} skipped (Pair is Locked).");
                return;
            }

            // --- NEW LOGGING: Check for Freeze ---
            if (manager.diplomacyFreezeTicks.TryGetValue(GetPairKey(facA, facB), out int expiry) && Find.TickManager.TicksGame < expiry)
            {
                if (Prefs.DevMode)
                    Log.Message($"[WorldDomination] Diplomacy: Change between {facA.Name} and {facB.Name} skipped (Relationship is Frozen).");
                return;
            }

            // SUCCESS PATH: This part is already in your code
            FactionRelationKind current = facA.RelationKindWith(facB);
            FactionRelationKind next;

            if (current == FactionRelationKind.Hostile) next = FactionRelationKind.Neutral;
            else if (current == FactionRelationKind.Ally) next = FactionRelationKind.Neutral;
            else next = Rand.Value < 0.5f ? FactionRelationKind.Hostile : FactionRelationKind.Ally;

            ForceDiplomacy(facA, facB, next, manager, Find.TickManager.TicksGame + 600000);

            string msg = GetDiplomacyFlavorText(facA, facB, current, next);
            var setA = Find.WorldObjects.Settlements.Where(s => s.Faction == facA).RandomElementWithFallback();

            // This adds it to the list in your UI
            manager.AddLog(new SpreadLogEntry(msg, setA, null));

            if (seth.notifyDiplomaticChange)
            {
                Find.LetterStack.ReceiveLetter("TSA_WD_Diplo_Change_Label".Translate(), msg, LetterDefOf.NeutralEvent, setA);
            }
        }

        private static string GetDiplomacyFlavorText(Faction facA, Faction facB, FactionRelationKind current, FactionRelationKind next)
        {
            string colorA = facA.Name.Colorize(Color.cyan);
            string colorB = facB.Name.Colorize(Color.cyan);
            if (next == FactionRelationKind.Hostile) return "TSA_WD_Diplo_Flavor_War".Translate(colorA, colorB);
            if (next == FactionRelationKind.Ally) return "TSA_WD_Diplo_Flavor_Ally".Translate(colorA, colorB);
            return "TSA_WD_Diplo_Flavor_Peace".Translate(colorA, colorB);
        }

        private static long GetPairKey(Faction a, Faction b)
        {
            int id1 = a.loadID;
            int id2 = b.loadID;
            return id1 < id2 ? (long)id1 << 32 | (uint)id2 : (long)id2 << 32 | (uint)id1;
        }
    }
}