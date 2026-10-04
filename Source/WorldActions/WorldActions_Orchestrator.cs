using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using TSA_WorldDomination.Settings;
using TSA_WorldDomination.WorldActions;
using TSA_WorldDomination.RaidLogic;

namespace WorldDomination
{
    public class WorldComponent_SpreadManager : WorldComponent
    {
        private Queue<Faction> dailyActionQueue = new Queue<Faction>();
        private int ticksPerAction = 0;
        private int ticksUntilNextAction = 0;
        private List<SpreadLogEntry> ActionLog = new List<SpreadLogEntry>();
        private const int MaxLogEntries = 500;
        private bool firstTickRun = false;
        private bool dirtyPowerStats = false; // Flag for immediate recalculation

        private Dictionary<int, float> factionThreats = new Dictionary<int, float>();
        private Dictionary<int, string> factionBreakdowns = new Dictionary<int, string>();

        // Diplomacy & Coalition Fields
        public Dictionary<long, int> diplomacyFreezeTicks = new Dictionary<long, int>();

        public Faction currentWorldLeader;
        public int leaderHandicapExpiryTick = -1;
        public int leaderHandicapCooldownTick = -1; // NEW: Individual Cooldown

        public Faction currentWeakestUnderdog;
        public int underdogBuffExpiryTick = -1;
        public int underdogBuffCooldownTick = -1; // NEW: Individual Cooldown

        public Faction expansionistZealFaction;
        public int expansionistZealExpiryTick = -1;
        public int expansionistZealCooldownTick = -1; // NEW: Individual Cooldown

        // Note: Coalition uses diplomacyFreezeTicks for its cooldown/duration logic
        public int antiLeaderCoalitionCooldownTick = -1; // NEW: Individual Cooldown

        // Caching
        public Dictionary<long, int> distanceCache = new Dictionary<long, int>();
        public HashSet<int> spaceTileCache = new HashSet<int>();

        public WorldComponent_SpreadManager(World world) : base(world) { }

        public void Notify_WeightsChanged() => dirtyPowerStats = true;

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            WorldActions_Utils.EnsureAllSettlementsInitialized();

            if (dailyActionQueue.Count == 0) CalculateDailyBudget();
            UpdateThreatScores();
        }

        public override void WorldComponentTick()
        {
            int currentTick = Find.TickManager.TicksGame;

            if (!firstTickRun) { UpdateThreatScores(); firstTickRun = true; }

            // Trigger update if dirty or on interval
            if (dirtyPowerStats || currentTick % 2500 == 0)
            {
                UpdateThreatScores();
                dirtyPowerStats = false;
            }

            if (currentTick % 60000 == 0) CalculateDailyBudget();

            if (dailyActionQueue.Count > 0)
            {
                ticksUntilNextAction--;
                if (ticksUntilNextAction <= 0)
                {
                    ExecuteNextAction();
                    ticksUntilNextAction = ticksPerAction;
                }
            }
        }

        private void CalculateDailyBudget()
        {
            dailyActionQueue.Clear();
            var seth = WorldDominationMod.settings;
            var factions = Find.FactionManager.AllFactionsListForReading
                .Where(f => f != null && !f.def.hidden && !f.defeated && !WorldActions_Utils.IsExcludedFaction(f));

            List<Faction> tempActionList = new List<Faction>();

            // Trigger World Events
            WorldActions_Revolt.TryTriggerRevolt(this);

            // --- 1. GLOBAL BALANCE CHECKS (Runs every day) --
            // Individual cooldowns are now handled inside each method via settings sliders
            WorldActions_DiplomacyBuffsNerfs.ApplyLeaderHandicap(this);
            WorldActions_DiplomacyBuffsNerfs.ApplyUnderdogBuff(this);
            WorldActions_DiplomacyBuffsNerfs.FormAntiLeaderCoalition(this);
            WorldActions_DiplomacyBuffsNerfs.ApplyExpansionistZeal(this);

            // Random Diplomacy Changes
            WorldActions_DiplomacyBuffsNerfs.TryChangeAllegiances(this);

            foreach (Faction f in factions)
            {
                float shares = 0;
                var settlements = Find.WorldObjects.Settlements.Where(s => s.Faction == f);
                foreach (var s in settlements)
                {
                    if (!WorldActions_Utils.IsWdSurfaceWorldObject(s)) continue;
                    var comp = s.GetComponent<CompViralSpread>();
                    if (comp == null) continue;

                    float baseShare = (comp.tier == SettlementTier.T4) ? seth.tier4Share :
                                      (comp.tier == SettlementTier.T3) ? seth.tier3Share :
                                      (comp.tier == SettlementTier.T2) ? seth.tier2Share : seth.tier1Share;

                    if (f == currentWeakestUnderdog && Find.TickManager.TicksGame < underdogBuffExpiryTick)
                    {
                        baseShare *= 2f;
                    }

                    shares += baseShare;
                }

                int actions = Math.Max(1, Mathf.RoundToInt(shares));
                for (int i = 0; i < actions; i++) tempActionList.Add(f);
            }

            tempActionList.Shuffle();
            foreach (Faction f in tempActionList) dailyActionQueue.Enqueue(f);

            int actionsCount = tempActionList.Count;
            ticksPerAction = actionsCount > 0 ? 60000 / actionsCount : 60000;
        }

        private void ExecuteNextAction()
        {
            if (dailyActionQueue.Count == 0) return;
            Faction f = dailyActionQueue.Dequeue();

            var actor = Find.WorldObjects.Settlements
                    .Where(s => s.Faction == f && !WorldActions_Utils.IsSettlementProtected(s) && !(s.GetComponent<CompViralSpread>()?.IsOnCooldown ?? true))
                    .RandomElementWithFallback();

            if (actor == null)
            {
                AddLog(new SpreadLogEntry("TSA_WD_Log_ActionSkip".Translate().ToString(), null)
                {
                    labelA = "TS_WD_Log_FactionLabel".Translate(f.Name).ToString()
                });
                return;
            }

            var comp = actor.GetComponent<CompViralSpread>();
            var seth = WorldDominationMod.settings;

            float incidentMult = 1f;
            if (f == currentWorldLeader && Find.TickManager.TicksGame < leaderHandicapExpiryTick) incidentMult = 2f;
            if (f == currentWeakestUnderdog && Find.TickManager.TicksGame < underdogBuffExpiryTick) incidentMult = 0.5f;

            float wRaid = seth.weightRaid;
            float wMinor = seth.weightMinorIncident * incidentMult;
            float wMajor = seth.weightMajorIncident * incidentMult;
            float wGrow = seth.weightGrow;
            float wBuildRoad = seth.weightBuildRoad;

            float totalWeight = wRaid + wMinor + wMajor + wGrow + wBuildRoad;
            if (totalWeight <= 0f) return;

            float rand = Rand.Range(0f, totalWeight);

            if (rand < wRaid)
                WorldActions_Raid.AttemptRaid(actor, comp, this);
            else if (rand < (wRaid + wMinor))
                WorldActions_Incidents.AttemptMinorIncident(actor, comp, this);
            else if (rand < (wRaid + wMinor + wMajor))
                WorldActions_Incidents.AttemptMajorIncident(actor, comp, this);
            else if (rand < (wRaid + wMinor + wMajor + wGrow))
                WorldActions_GrowthExpand.AttemptGrow(actor, comp, this);
            else
                WorldActions_Roads.AttemptBuildRoad(actor, comp, this);
        }

        public void AddLog(SpreadLogEntry entry)
        {
            ActionLog.Add(entry);
            if (ActionLog.Count > MaxLogEntries) ActionLog.RemoveAt(0);
        }

        public float GetThreatScoreFor(Faction f) => (f != null && factionThreats.TryGetValue(f.loadID, out float s)) ? s : 0.5f;
        public string GetThreatBreakdown(Faction f) => (f != null && factionBreakdowns.TryGetValue(f.loadID, out string b)) ? b : "No data.";

        private void UpdateThreatScores()
        {
            var seth = WorldDominationMod.settings;
            var playerBases = Find.WorldObjects.Settlements
                .Where(s => s.Faction?.IsPlayer == true && s.Tile >= 0 && WorldActions_Utils.IsWdSurfaceTile(s.Tile))
                .Select(s => new { s.Tile, Radius = WorldActions_Utils.GetPlayerInfluenceRadius(s.Tile) })
                .ToList();

            if (!playerBases.Any()) return;

            var enemyBasesByFaction = Find.WorldObjects.Settlements
                .Where(s => s.Faction != null && !s.Faction.IsPlayer && !s.Faction.def.hidden && s.Tile >= 0
                    && WorldActions_Utils.IsWdSurfaceWorldObject(s))
                .ToLookup(s => s.Faction);

            foreach (var f in Find.FactionManager.AllFactionsVisible.Where(x => !x.IsPlayer && !x.def.hidden))
            {
                float totalPoints = 0f;
                StringBuilder sb = new StringBuilder();

                foreach (var s in enemyBasesByFaction[f])
                {
                    var comp = s.GetComponent<CompViralSpread>();
                    foreach (var pb in playerBases)
                    {
                        int dist = WorldActions_Utils.GetDistance(pb.Tile, s.Tile, this);
                        if (dist >= 0 && dist <= pb.Radius)
                        {
                            float tierMult = (comp?.tier == SettlementTier.T4) ? seth.threatTier4Mult :
                                             (comp?.tier == SettlementTier.T3) ? seth.threatTier3Mult :
                                             (comp?.tier == SettlementTier.T2) ? seth.threatTier2Mult : seth.threatTier1Mult;

                            float distPct = (float)dist / pb.Radius;
                            float distMult = (distPct <= 0.5f) ? seth.threatDistMult50 :
                                             (distPct <= 0.75f) ? seth.threatDistMult75 : seth.threatDistMult100;

                            float finalVal = tierMult * distMult;
                            totalPoints += finalVal;

                            int tNum = comp?.tier == SettlementTier.T4 ? 4 : (comp?.tier == SettlementTier.T3 ? 3 : (comp?.tier == SettlementTier.T2 ? 2 : 1));
                            sb.AppendLine("TSA_WD_Threat_BreakdownLine".Translate(s.LabelCap, finalVal.ToString("F2"), tNum, dist, pb.Radius.ToString("F0")).ToString());
                            break;
                        }
                    }
                }

                float finalMult = Math.Max(seth.minRaidPointsMultiplier, totalPoints);
                factionThreats[f.loadID] = finalMult;
                factionBreakdowns[f.loadID] = "TSA_WD_Threat_BreakdownHeader".Translate(finalMult.ToString("F2"), seth.minRaidPointsMultiplier.ToString("F2")).ToString() + (sb.Length > 0 ? sb.ToString() : "TSA_WD_None".Translate().ToString());
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref ActionLog, "ActionLog", LookMode.Deep);
            Scribe_Collections.Look(ref factionThreats, "factionThreats", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref factionBreakdowns, "factionBreakdowns", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref diplomacyFreezeTicks, "diplomacyFreezeTicks", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref distanceCache, "distanceCache", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref spaceTileCache, "spaceTileCache", LookMode.Value);

            Scribe_References.Look(ref currentWorldLeader, "currentWorldLeader");
            Scribe_References.Look(ref currentWeakestUnderdog, "currentWeakestUnderdog");

            Scribe_References.Look(ref expansionistZealFaction, "expansionistZealFaction");
            Scribe_Values.Look(ref expansionistZealExpiryTick, "expansionistZealExpiryTick", -1);
            Scribe_Values.Look(ref expansionistZealCooldownTick, "expansionistZealCooldownTick", -1);

            Scribe_Values.Look(ref leaderHandicapExpiryTick, "leaderHandicapExpiryTick", -1);
            Scribe_Values.Look(ref leaderHandicapCooldownTick, "leaderHandicapCooldownTick", -1);

            Scribe_Values.Look(ref underdogBuffExpiryTick, "underdogBuffExpiryTick", -1);
            Scribe_Values.Look(ref underdogBuffCooldownTick, "underdogBuffCooldownTick", -1);

            Scribe_Values.Look(ref antiLeaderCoalitionCooldownTick, "antiLeaderCoalitionCooldownTick", -1);

            Scribe_Values.Look(ref ticksUntilNextAction, "ticksUntilNextAction", 0);
            Scribe_Values.Look(ref ticksPerAction, "ticksPerAction", 0);
            Scribe_Values.Look(ref firstTickRun, "firstTickRun", false);

            if (diplomacyFreezeTicks == null) diplomacyFreezeTicks = new Dictionary<long, int>();
            if (distanceCache == null) distanceCache = new Dictionary<long, int>();
            if (spaceTileCache == null) spaceTileCache = new HashSet<int>();
            if (factionThreats == null) factionThreats = new Dictionary<int, float>();
            if (factionBreakdowns == null) factionBreakdowns = new Dictionary<int, string>();
        }

        public List<SpreadLogEntry> GetLog() => ActionLog;
    }
}