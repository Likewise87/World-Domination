using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using Outposts;
using System.Reflection;
using System.Text;
using System.Linq;
using TSA_WorldDomination.Settings;

namespace WorldDomination
{
    public enum SettlementTier { T1, T2, T3, T4 }

    public class CompProperties_ViralSpread : WorldObjectCompProperties
    {
        public CompProperties_ViralSpread() => this.compClass = typeof(CompViralSpread);
    }

    public class CompViralSpread : WorldObjectComp
    {
        public SettlementTier tier = SettlementTier.T1;
        public string subType = "";
        public float strength;

        public int incidentCooldownUntilTick = -1;
        public int espionageCooldownUntilTick = -1;
        public int aidCooldownUntilTick = -1;

        public float cachedRadius = -1f;
        private int lastRadiusUpdateTick = -9999;

        private bool outpostInitialized = false;
        /// <summary>After tile is valid, strip comp on confirmed orbit (init can run before layer exists).</summary>
        private bool evaluatedDeferredOrbitStrip;
        /// <summary>Fix saves where player was marked Excluded via old IsExcludedFaction(player).</summary>
        private bool repairedMisclassifiedPlayerSubType;
        private int ticksExisted = 0;

        public int roadTargetTile = -1;
        public float roadProgress = 0f;
        public string roadTargetName = string.Empty;
        public SettlementTier selectedRoadTier = SettlementTier.T1;

        public int redirectionTargetTile = -1;

        public bool IsIncidentOnCooldown => Find.TickManager.TicksGame < incidentCooldownUntilTick;
        public bool IsEspionageOnCooldown => Find.TickManager.TicksGame < espionageCooldownUntilTick;
        public bool IsAidOnCooldown => Find.TickManager.TicksGame < aidCooldownUntilTick;
        public bool IsOnCooldown => IsIncidentOnCooldown || IsEspionageOnCooldown;

        public bool IsSettlement => parent is Settlement;
        public bool IsOutpost => parent is Outpost;

        public override void Initialize(WorldObjectCompProperties props)
        {
            base.Initialize(props);

            if (parent != null && WorldActions_Utils.IsConfirmedOrbitWorldObject(parent))
            {
                parent.AllComps.Remove(this);
                return;
            }

            // IsExcludedFaction returns true for player; do not skip Colony/Outpost setup.
            if (parent.Faction != null && WorldActions_Utils.IsExcludedFaction(parent.Faction) && !parent.Faction.IsPlayer)
            {
                this.subType = "Excluded";
                this.outpostInitialized = true;
                return;
            }

            if (string.IsNullOrEmpty(subType))
            {
                // SURGICAL FIX: Handle Vassals first
                if (WorldActions_Utils.IsVassal(parent))
                {
                    subType = "Vassal";
                    strength = 400f; // Initialized to max/high strength
                    outpostInitialized = true;
                    return;
                }

                if (IsOutpost)
                {
                    subType = "Outpost";
                    strength = 0f;
                }
                else if (IsSettlement)
                {
                    bool isPlayerSettlement = parent.Faction != null && parent.Faction.IsPlayer;
                    if (isPlayerSettlement)
                    {
                        subType = "Colony";
                        strength = 0;

                        var seth = WorldDominationMod.settings;
                        if (seth != null)
                        {
                            incidentCooldownUntilTick = Find.TickManager.TicksGame + Mathf.RoundToInt(seth.cooldownPlayerRaidDays * 60000f);
                        }
                    }
                    else
                    {
                        var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
                        if (manager != null) WorldActions_Utils.ApplyRandomTier(this);
                        else SetState(SettlementTier.T1);
                    }
                }
            }
        }

        public void UpdateOutpostStrengthLogically()
        {
            // Vassals handle their own strength logic, do not override them via roster changes
            if (subType == "Vassal") return;

            if (!IsOutpost) return;
            float newMax = GetTargetOutpostStrength();
            strength = newMax;
            outpostInitialized = true;
            lastRadiusUpdateTick = -9999;
        }

        public override void CompTick()
        {
            base.CompTick();

            if (!evaluatedDeferredOrbitStrip && parent != null && parent.Spawned && parent.Tile >= 0
                && Find.WorldGrid != null && parent.Tile < Find.WorldGrid.TilesCount)
            {
                evaluatedDeferredOrbitStrip = true;
                if (WorldActions_Utils.IsConfirmedOrbitWorldObject(parent))
                {
                    parent.AllComps.Remove(this);
                    return;
                }
            }

            if (!repairedMisclassifiedPlayerSubType && subType == "Excluded" && parent.Faction != null && parent.Faction.IsPlayer)
            {
                repairedMisclassifiedPlayerSubType = true;
                if (IsOutpost)
                {
                    subType = "Outpost";
                    strength = 0f;
                    UpdateOutpostStrengthLogically();
                }
                else if (IsSettlement)
                {
                    subType = "Colony";
                    strength = 0f;
                }
            }

            // 1. ROAD CONSTRUCTION HEARTBEAT
            // Only runs if a target is set and the parent is an Outpost.
            if (roadTargetTile != -1 && IsOutpost)
            {
                // 500 strength = 1.0 (100%) progress per 60,000 ticks.
                // Incremental progress per tick = strength / (500 * 60,000)
                float progressThisTick = strength / 30000000f;
                roadProgress += progressThisTick;

                // When progress hits 100%, build the next tile segment.
                if (roadProgress >= 1f)
                {
                    roadProgress = 0f;
                    TSA_WorldDomination.WorldActions.WorldActions_Roads.TryBuildRoadStepToTile((Outpost)parent, roadTargetTile);
                }
            }

            // 2. PLAYER INFLUENCE CACHING
            if (parent.Faction?.IsPlayer == true && IsSettlement)
            {
                int currentTick = Find.TickManager.TicksGame;
                if (cachedRadius < 0 || currentTick >= lastRadiusUpdateTick + 2500)
                {
                    var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
                    cachedRadius = (manager != null) ? WorldActions_Utils.GetPlayerInfluenceRadius(parent.Tile) : 0f;
                    lastRadiusUpdateTick = currentTick;
                }
            }

            // 3. OUTPOST INITIALIZATION DELAY
            if (IsOutpost && !outpostInitialized && subType != "Vassal")
            {
                ticksExisted++;
                if (ticksExisted > 120)
                {
                    float targetStrength = GetTargetOutpostStrength();
                    if (targetStrength > 100f)
                    {
                        this.strength = targetStrength;
                        this.outpostInitialized = true;
                    }
                    else if (ticksExisted > 1000)
                    {
                        this.strength = 100f;
                        this.outpostInitialized = true;
                    }
                }
            }

            // 4. THREAT NOTIFICATION
            if (Find.TickManager.TicksGame % 10000 == 0)
            {
                Find.World.GetComponent<WorldThreatManager>()?.Notify_ThreatChanged();
            }

            // 5. DAILY RECOVERY LOGIC (Once every 60,000 ticks)
            if (Find.TickManager.TicksGame % 60000 == 0)
            {
                if (subType == "Vassal")
                {
                    // Regains 100 strength per day up to a cap of 500
                    strength = Mathf.Min(strength + 100f, 500f);
                }
                else if (IsOutpost)
                {
                    float targetStr = GetTargetOutpostStrength();
                    if (strength > targetStr) strength = targetStr;
                    else if (strength < targetStr)
                    {
                        float gain = Math.Max(50f, targetStr * 0.15f);
                        strength = Mathf.Min(strength + gain, targetStr);
                    }
                }
            }

        }

        public float GetTargetOutpostStrength()
        {
            if (subType == "Vassal") return 500f;

            if (parent is Outpost outpost)
            {
                FieldInfo occupantsField = typeof(Outpost).GetField("occupants", BindingFlags.Instance | BindingFlags.NonPublic);
                if (occupantsField != null)
                {
                    List<Pawn> occupants = occupantsField.GetValue(outpost) as List<Pawn>;
                    if (occupants != null && occupants.Count > 0)
                    {
                        float totalStrength = 0f;
                        foreach (Pawn pawn in occupants)
                        {
                            if (pawn.Faction != null && pawn.Faction.IsPlayer)
                            {
                                int shooting = pawn.skills?.GetSkill(SkillDefOf.Shooting).Level ?? 0;
                                int melee = pawn.skills?.GetSkill(SkillDefOf.Melee).Level ?? 0;
                                int maxSkill = Math.Max(shooting, melee);
                                float basePawnStrength = 50f + (maxSkill * 7.5f);
                                float healthFactor = pawn.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
                                totalStrength += (basePawnStrength * healthFactor);
                            }
                        }
                        return Mathf.Min(totalStrength, 1500f);
                    }
                }
            }
            return 100f;
        }

        public void SetState(SettlementTier newTier)
        {
            if (IsOutpost || subType == "Vassal") return;
            SettlementTier oldTier = tier;
            tier = newTier;
            subType = GetRandomSubType(newTier);
            var range = GetStrengthRange(newTier);
            if (strength <= 0) strength = range.RandomInRange;
            lastRadiusUpdateTick = -9999;
            if (oldTier != newTier)
                Find.World?.GetComponent<Text_WorldTierOnSettlements>()?.NotifyTierLabelCacheDirty();
        }

        public void AddStrength(float amount)
        {
            strength += amount;
            // Cap Vassals specifically if needed
            if (subType == "Vassal") strength = Mathf.Min(strength, 500f);
            if (IsSettlement && subType != "Vassal") CheckTierUpdate();
        }

        public void ReduceStrength(float percentage)
        {
            strength -= strength * percentage;
            if (IsSettlement && strength < 100f) strength = 100f;
            if (IsSettlement && subType != "Vassal") CheckTierUpdate();
        }

        public void CheckTierUpdate()
        {
            if (IsOutpost || subType == "Vassal") return;
            SettlementTier oldTier = tier;

            if (strength > 1600f) tier = SettlementTier.T4;
            else if (strength > 1000f) tier = SettlementTier.T3;
            else if (strength > 500f) tier = SettlementTier.T2;
            else tier = SettlementTier.T1;

            if (strength > 2250f) strength = 2250f;

            if (oldTier != tier)
            {
                subType = GetRandomSubType(tier);
                lastRadiusUpdateTick = -9999;
                Find.World?.GetComponent<Text_WorldTierOnSettlements>()?.NotifyTierLabelCacheDirty();
            }
        }

        public static FloatRange GetStrengthRange(SettlementTier t)
        {
            if (t == SettlementTier.T4) return new FloatRange(1601f, 2250f);
            if (t == SettlementTier.T3) return new FloatRange(1001f, 1600f);
            if (t == SettlementTier.T2) return new FloatRange(501f, 1000f);
            return new FloatRange(100f, 500f);
        }

        private string GetRandomSubType(SettlementTier forTier)
        {
            switch (forTier)
            {
                case SettlementTier.T1:
                    return (new List<string> { "Logging", "Mining", "Farming" }).RandomElement();
                case SettlementTier.T2:
                    return (new List<string> { "Production", "Slavery" }).RandomElement();
                case SettlementTier.T3:
                    return "Fortress";
                case SettlementTier.T4:
                    return "Citadel";
                default:
                    return "Generic";
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref tier, "tier", SettlementTier.T1);
            Scribe_Values.Look(ref subType, "subType", "");
            Scribe_Values.Look(ref strength, "strength", 100f);
            Scribe_Values.Look(ref incidentCooldownUntilTick, "incidentCooldownUntilTick", -1);
            Scribe_Values.Look(ref espionageCooldownUntilTick, "espionageCooldownUntilTick", -1);
            Scribe_Values.Look(ref aidCooldownUntilTick, "aidCooldownUntilTick", -1);
            Scribe_Values.Look(ref outpostInitialized, "outpostInitialized", false);
            Scribe_Values.Look(ref ticksExisted, "ticksExisted", 0);

            // SURGICAL ADDITION: Road building state
            Scribe_Values.Look(ref roadTargetTile, "roadTargetTile", -1);
            Scribe_Values.Look(ref roadProgress, "roadProgress", 0f);
            Scribe_Values.Look(ref roadTargetName, "roadTargetName", string.Empty);
            Scribe_Values.Look(ref selectedRoadTier, "selectedRoadTier", SettlementTier.T1);

            Scribe_Values.Look(ref redirectionTargetTile, "redirectionTargetTile", -1);
        }

        public override string CompInspectStringExtra()
        {
            StringBuilder sb = new StringBuilder();

            // 1. BASE TYPE INFO (Settlement vs Outpost)
            if (IsSettlement)
            {
                // SURGICAL FIX: Custom label for Vassals in inspect string
                if (subType == "Vassal")
                {
                    sb.Append("TSA_WD_Inspect_Vassal".Translate(strength.ToString("F0"), "500"));
                }
                else if (parent.Faction != null && parent.Faction.IsPlayer)
                {
                    sb.Append("TSA_WD_Inspect_PlayerColony".Translate());
                }
                else
                {
                    string tierKey = (tier == SettlementTier.T4) ? "TSA_WD_Tier4" :
                                     (tier == SettlementTier.T3) ? "TSA_WD_Tier3" :
                                     (tier == SettlementTier.T2) ? "TSA_WD_Tier2" : "TSA_WD_Tier1";
                    string subTypeKey = "TSA_WD_SubType_" + subType;
                    sb.Append("TSA_WD_Inspect_Settlement".Translate(tierKey.Translate(), subTypeKey.Translate(), strength.ToString("F0")));
                }
            }
            else
            {
                sb.Append("TSA_WD_Inspect_Outpost".Translate(strength.ToString("F0"), GetTargetOutpostStrength().ToString("F0")));

                // SURGICAL FIX: Append the actual progress string
                if (roadTargetTile != -1)
                {
                    sb.AppendLine();
                    // Inside the roadTargetTile check:
                    string roadTypeLabel = selectedRoadTier == SettlementTier.T4 ? "TSA_WD_RoadAsphalt".Translate() :
                                           selectedRoadTier == SettlementTier.T3 ? "TSA_WD_RoadAsphalt".Translate() :
                                           selectedRoadTier == SettlementTier.T2 ? "TSA_WD_RoadStone".Translate() :
                                           "TSA_WD_RoadDirt".Translate();

                    // This was missing:
                    sb.Append("TSA_WD_Inspect_RoadStatus".Translate(roadTypeLabel, (roadProgress * 100f).ToString("F0"), roadTargetName));
                }
            }

            // 2. PLAYER INFLUENCE RADIUS
            if (parent.Faction != null && parent.Faction.IsPlayer && IsSettlement && subType != "Vassal")
            {
                sb.AppendLine();
                sb.Append("TSA_WD_Inspect_Influence".Translate(cachedRadius.ToString("F1")));
            }

            // 3. COOLDOWN LOGIC
            int ticksNow = Find.TickManager.TicksGame;
            if (IsEspionageOnCooldown)
            {
                float daysLeft = (espionageCooldownUntilTick - ticksNow) / 60000f;
                sb.AppendLine();
                sb.Append("TSA_WD_Inspect_EspionageCD".Translate(daysLeft.ToString("F1")).Colorize(Color.red));
            }
            if (IsIncidentOnCooldown)
            {
                float daysLeft = (incidentCooldownUntilTick - ticksNow) / 60000f;
                sb.AppendLine();
                sb.Append("TSA_WD_Inspect_GlobalCD".Translate(daysLeft.ToString("F1")).Colorize(Color.yellow));
            }
            if (IsAidOnCooldown)
            {
                float daysLeft = (aidCooldownUntilTick - ticksNow) / 60000f;
                sb.AppendLine();
                sb.Append("TSA_WD_Inspect_AidCD".Translate(daysLeft.ToString("F1")).Colorize(Color.gray));
            }

            // 4. NPC ATTACK RADIUS
            if (parent.Faction != null && !parent.Faction.IsPlayer && IsSettlement)
            {
                var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
                if (manager != null && WorldActions_Utils.IsWdSurfaceTile(parent.Tile))
                {
                    var seth = WorldDominationMod.settings;
                    float range = seth.raidTargetRadius;

                    bool hasZeal = parent.Faction == manager.expansionistZealFaction &&
                                   Find.TickManager.TicksGame < manager.expansionistZealExpiryTick;

                    if (hasZeal) range *= 2f;

                    sb.AppendLine();
                    string rangeLabel = hasZeal ? "TSA_WD_Inspect_AttackRange_Zeal" : "TSA_WD_Inspect_AttackRange";
                    sb.Append(rangeLabel.Translate(range.ToString("F0")).Colorize(hasZeal ? Color.cyan : Color.white));
                }
            }

            return sb.ToString().Trim();
        }
    }
}