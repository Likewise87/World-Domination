using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TSA_WorldDomination.Settings
{
    public class RaidOutcome : IExposable
    {
        public float threshold = 1.0f;
        public float winChance = 0.5f;
        public float attLossWin = 0.1f;
        public float defLossLoss = 0.2f;
        public float attLossLoss = 0.2f;
        public float defLossWin = 0.05f;

        public void ExposeData()
        {
            Scribe_Values.Look(ref threshold, "threshold", 1.0f);
            Scribe_Values.Look(ref winChance, "winChance", 0.5f);
            Scribe_Values.Look(ref attLossWin, "attLossWin", 0.1f);
            Scribe_Values.Look(ref defLossLoss, "defLossLoss", 0.2f);
            Scribe_Values.Look(ref attLossLoss, "attLossLoss", 0.2f);
            Scribe_Values.Look(ref defLossWin, "defLossWin", 0.05f);
        }
    }

    public class WorldDominationSettings : ModSettings
    {
        // ==========================================
        // SOURCE OF TRUTH: DEFAULT VALUES
        // ==========================================

        // Diplomacy Lock in
        public bool initialAllegianceLockDone = false;

        // --- MAIN PAGE ---
        public const float DefTier1Share = 0.3f;
        public const float DefTier2Share = 0.5f;
        public const float DefTier3Share = 0.8f;
        public const float DefTier4Share = 1.0f;
        public const float DefWeightGrow = 120f;
        public const float DefWeightRaid = 15f;
        public const float DefWeightMinorIncident = 16f;
        public const float DefWeightMajorIncident = 2f;
        public const float DefWeightBuildRoad = 5f;
        public const float DefCdGrowDays = 0.2f;
        public const float DefCdExpandDays = 1.0f;
        public const float DefCdRaidDays = 1.0f;
        public const float DefCdBeingRaidedDays = 0.1f;

        // --- GROWTH & EXPANSION PAGE ---
        public const int DefMaxSettlements = 250;
        public const float DefBaseGrowth = 100f;
        public const float DefGrowthScaling = 0.5f;
        public const int DefExpandMinRad = 5;
        public const int DefExpandMaxRad = 12;
        public const int DefMaxRoadRange = 16;
        public const float DefMinorIncSev = 150f;
        public const float DefMajorIncSev = 450f;
        public const int DefLocalMaxT1 = 9;
        public const int DefLocalMaxT2 = 5;
        public const int DefLocalMaxT3 = 3;
        public const int DefLocalMaxT4 = 1;
        public const float DefExpansionSuccessChance = 0.30f;

        // --- DIPLOMACY PAGE ---
        public const float DefRevoltChance = 0.02f;
        public const float DefDiplomacyChangeChance = 0.03f;
        public const bool DefEnableLeaderHandicap = true;
        public const bool DefEnableUnderdogBuff = true;
        public const bool DefEnableAntiLeaderCoalition = true;
        public const bool DefEnableRandomDiplomacy = true;
        public const bool DefEnableExpansionistZeal = true;

        public const float DefDurLeaderHandicapDays = 10f;
        public const float DefCdLeaderHandicapDays = 15f;
        public const float DefDurUnderdogBuffDays = 10f;
        public const float DefCdUnderdogBuffDays = 15f;
        public const float DefDurExpansionistZealDays = 10f;
        public const float DefCdExpansionistZealDays = 15f;
        public const float DefDurAntiLeaderCoalitionDays = 15f;
        public const float DefCdAntiLeaderCoalitionDays = 20f;

        public const float DefZealTriggerChance = 0.15f;

        //Matrix Diplomacy changes
        public HashSet<string> lockedAllegiancePairs = new HashSet<string>();

        // --- PLAYER THREAT PAGE ---
        public const float DefPlayerInfBase = 4f;
        public const float DefPlayerInfWealthRate = 0.75f;
        public const float DefPlayerInfTimeRate = 0.10f;
        public const int DefMinRaidPoints = 100;
        public const int DefMaxRaidPoints = 10000;
        public const float DefMinRaidPointsMult = 0.5f;
        public const float DefMaxThreatMult = 3.0f;
        public const float DefThreatDist50 = 1.0f;
        public const float DefThreatDist75 = 0.75f;
        public const float DefThreatDist100 = 0.5f;
        public const float DefThreatTier1Mult = 0.5f;
        public const float DefThreatTier2Mult = 0.75f;
        public const float DefThreatTier3Mult = 1.00f;
        public const float DefThreatTier4Mult = 1.25f;

        // --- WORLD RAIDS PAGE ---
        public const bool DefAllowPlayerRaid = true;
        public const bool DefAllowPlayerOutpostRaid = true;
        public const float DefCdPlayerRaidDays = 5.0f;
        public const float DefRaidCostPct = 0.15f;
        public const float DefRaidTargetRadius = 25f;
        public const float DefRaidAttackerAllyRadius = 15f;
        public const float DefRaidDefenderAllyRadius = 20f;
        public const float DefMinRaidRatio = 0.9f;
        public const float DefRazeChance = 0.10f;

        // --- SABOTAGE PAGE ---
        public const float DefWeightSabSuccess = 37f;
        public const float DefWeightSabCleanFail = 32f;
        public const float DefWeightSabInjuredFail = 25f;
        public const float DefWeightSabFatalFail = 6f;
        public const float DefSabSkillSuccessWeightBonus = 5.0f;
        public const float DefSabTierSuccessWeightPenalty = 5.0f;
        public const float DefSabHealthImpactWeight = 1.0f;
        public const float DefSabSocialCleanBonus = 0.02f;
        public const float DefSabCombatSurvivalBonus = 0.02f;
        public const float DefSabBaseReduc = 225f;
        public const float DefSabSkillReductionBonus = 20f;
        public const float DefSabCdDays = 5.0f;

        // --- DISINFORMATION PAGE ---
        public const float DefWeightDisSuccess = 40f;
        public const float DefWeightDisCleanFail = 30f;
        public const float DefWeightDisInjuredFail = 25f;
        public const float DefWeightDisFatalFail = 5f;
        public const float DefDisSkillSuccessWeightBonus = 5.0f;
        public const float DefDisTierSuccessWeightPenalty = 5.0f;
        public const float DefDisHealthImpactWeight = 1.0f;
        public const float DefDisSocialCleanBonus = 0.02f;
        public const float DefDisCombatSurvivalBonus = 0.02f;
        public const float DefDisBaseReduc = 150f;
        public const float DefDisSkillReductionBonus = 15f;
        public const float DefDisCdDays = 5.0f;

        // --- FOOD LOGISTICS PAGE ---
        public const bool DefFoodLogisticsActive = true;
        public const float DefFoodConsumptionPerPawn = 1.0f;
        public const float DefFoodProductionPerSkill = 0.4f;
        public const int DefMaxLogisticsRange = 20;
        public const float DefEffAt60 = 1.0f;
        public const float DefEffAt80 = 0.85f;
        public const float DefEffAt100 = 0.70f;

        // --- NOTIFICATIONS PAGE ---
        public const bool DefNotifyNewSettlement = true;
        public const bool DefNotifySettlementRaided = true;
        public const bool DefNotifyOutpostDestroyed = true;
        public const bool DefNotifyThreatLevel = true;
        public const bool DefNotifyDiplomaticChange = true;
        public const bool DefNotifyBuffNerf = true;

        // --- World Generation PAGE ---
        public const float DefGenWeightT1 = 150.0f;
        public const float DefGenWeightT2 = 45.0f;
        public const float DefGenWeightT3 = 4.0f;
        public const float DefGenWeightT4 = 1.0f;

        // --- GARRISON SETTINGS (KCSG) ---
        public const float DefKcsgMultTribalT1 = 0.5f;
        public const float DefKcsgMultTribalT2 = 0.75f;
        public const float DefKcsgMultTribalT3 = 1.6f;
        public const float DefKcsgMultTribalT4 = 3.2f;
        public const float DefKcsgMultGenericT1 = 0.4f;
        public const float DefKcsgMultGenericT2 = 0.75f;
        public const float DefKcsgMultGenericT3 = 1.3f;
        public const float DefKcsgMultGenericT4 = 2.5f;

        // ==========================================
        // SETTINGS VARIABLES
        // ==========================================

        public float tier1Share = DefTier1Share;
        public float tier2Share = DefTier2Share;
        public float tier3Share = DefTier3Share;
        public float tier4Share = DefTier4Share;
        public float weightGrow = DefWeightGrow;
        public float weightRaid = DefWeightRaid;
        public float weightMinorIncident = DefWeightMinorIncident;
        public float weightMajorIncident = DefWeightMajorIncident;
        public float weightBuildRoad = DefWeightBuildRoad;
        public float cooldownGrowDays = DefCdGrowDays;
        public float cooldownExpandDays = DefCdExpandDays;
        public float cooldownRaidDays = DefCdRaidDays;
        public float cooldownBeingRaidedDays = DefCdBeingRaidedDays;

        public int maxSettlements = DefMaxSettlements;
        public float baseGrowthAmount = DefBaseGrowth;
        public float growthScalingIntensity = DefGrowthScaling;
        public int expandMinRadius = DefExpandMinRad;
        public int expandMaxRadius = DefExpandMaxRad;
        public int maxRoadRange = DefMaxRoadRange;
        public float minorIncidentSeverity = DefMinorIncSev;
        public float majorIncidentSeverity = DefMajorIncSev;
        public int localMaxT1 = DefLocalMaxT1;
        public int localMaxT2 = DefLocalMaxT2;
        public int localMaxT3 = DefLocalMaxT3;
        public int localMaxT4 = DefLocalMaxT4;
        public float expansionSuccessChance = DefExpansionSuccessChance;

        public float revoltChance = DefRevoltChance;
        public float diplomacyChangeChance = DefDiplomacyChangeChance;
        public bool enableLeaderHandicap = DefEnableLeaderHandicap;
        public bool enableUnderdogBuff = DefEnableUnderdogBuff;
        public bool enableAntiLeaderCoalition = DefEnableAntiLeaderCoalition;
        public bool enableRandomDiplomacy = DefEnableRandomDiplomacy;
        public bool enableExpansionistZeal = DefEnableExpansionistZeal;

        public float durLeaderHandicapDays = DefDurLeaderHandicapDays;
        public float cdLeaderHandicapDays = DefCdLeaderHandicapDays;
        public float durUnderdogBuffDays = DefDurUnderdogBuffDays;
        public float cdUnderdogBuffDays = DefCdUnderdogBuffDays;
        public float durExpansionistZealDays = DefDurExpansionistZealDays;
        public float cdExpansionistZealDays = DefCdExpansionistZealDays;
        public float durAntiLeaderCoalitionDays = DefDurAntiLeaderCoalitionDays;
        public float cdAntiLeaderCoalitionDays = DefCdAntiLeaderCoalitionDays;
        public float zealTriggerChance = DefZealTriggerChance;

        public float playerInfluenceBase = DefPlayerInfBase;
        public float playerInfluenceWealthRate = DefPlayerInfWealthRate;
        public float playerInfluenceTimeRate = DefPlayerInfTimeRate;
        public float minRaidPoints = DefMinRaidPoints;
        public float maxRaidPoints = DefMaxRaidPoints;
        public float minRaidPointsMultiplier = DefMinRaidPointsMult;
        public float maxThreatMultiplier = DefMaxThreatMult;
        public float threatDistMult50 = DefThreatDist50;
        public float threatDistMult75 = DefThreatDist75;
        public float threatDistMult100 = DefThreatDist100;
        public float threatTier1Mult = DefThreatTier1Mult;
        public float threatTier2Mult = DefThreatTier2Mult;
        public float threatTier3Mult = DefThreatTier3Mult;
        public float threatTier4Mult = DefThreatTier4Mult;

        public bool allowPlayerRaid = DefAllowPlayerRaid;
        public bool allowPlayerOutpostRaid = DefAllowPlayerOutpostRaid;
        public float cooldownPlayerRaidDays = DefCdPlayerRaidDays;
        public float raidCostPct = DefRaidCostPct;
        public float raidTargetRadius = DefRaidTargetRadius;
        public float raidAttackerAllyRadius = DefRaidAttackerAllyRadius;
        public float raidDefenderAllyRadius = DefRaidDefenderAllyRadius;
        public float minRaidRatio = DefMinRaidRatio;
        public float razeChance = DefRazeChance;
        public List<RaidOutcome> raidOutcomes = new List<RaidOutcome>();

        public float weightSabSuccess = DefWeightSabSuccess;
        public float weightSabCleanFail = DefWeightSabCleanFail;
        public float weightSabInjuredFail = DefWeightSabInjuredFail;
        public float weightSabFatalFail = DefWeightSabFatalFail;
        public float sabotageSkillSuccessWeightBonus = DefSabSkillSuccessWeightBonus;
        public float sabotageTierSuccessWeightPenalty = DefSabTierSuccessWeightPenalty;
        public float sabotageHealthImpactWeight = DefSabHealthImpactWeight;
        public float sabotageSocialCleanBonus = DefSabSocialCleanBonus;
        public float sabotageCombatSurvivalBonus = DefSabCombatSurvivalBonus;
        public float sabotageBaseReduction = DefSabBaseReduc;
        public float sabotageSkillReductionBonus = DefSabSkillReductionBonus;
        public float sabotageCooldownDays = DefSabCdDays;

        public float weightDisSuccess = DefWeightDisSuccess;
        public float weightDisCleanFail = DefWeightDisCleanFail;
        public float weightDisInjuredFail = DefWeightDisInjuredFail;
        public float weightDisFatalFail = DefWeightDisFatalFail;
        public float disSkillSuccessWeightBonus = DefDisSkillSuccessWeightBonus;
        public float disTierSuccessWeightPenalty = DefDisTierSuccessWeightPenalty;
        public float disHealthImpactWeight = DefDisHealthImpactWeight;
        public float disSocialCleanBonus = DefDisSocialCleanBonus;
        public float disCombatSurvivalBonus = DefDisCombatSurvivalBonus;
        public float disBaseReduction = DefDisBaseReduc;
        public float disSkillReductionBonus = DefDisSkillReductionBonus;
        public float disCooldownDays = DefDisCdDays;

        public bool foodLogisticsActive = DefFoodLogisticsActive;
        public float foodConsumptionPerPawn = DefFoodConsumptionPerPawn;
        public float foodProductionPerSkill = DefFoodProductionPerSkill;
        public int maxLogisticsRange = DefMaxLogisticsRange;
        public float effAt60 = DefEffAt60;
        public float effAt80 = DefEffAt80;
        public float effAt100 = DefEffAt100;

        public bool notifyNewSettlement = DefNotifyNewSettlement;
        public bool notifySettlementRaided = DefNotifySettlementRaided;
        public bool notifyOutpostDestroyed = DefNotifyOutpostDestroyed;
        public bool notifyThreatLevel = DefNotifyThreatLevel;
        public bool notifyDiplomaticChange = DefNotifyDiplomaticChange;
        public bool notifyBuffNerf = DefNotifyBuffNerf;

        public float genWeightT1 = DefGenWeightT1;
        public float genWeightT2 = DefGenWeightT2;
        public float genWeightT3 = DefGenWeightT3;
        public float genWeightT4 = DefGenWeightT4;

        public float kcsgMultTribalT1 = DefKcsgMultTribalT1;
        public float kcsgMultTribalT2 = DefKcsgMultTribalT2;
        public float kcsgMultTribalT3 = DefKcsgMultTribalT3;
        public float kcsgMultTribalT4 = DefKcsgMultTribalT4;
        public float kcsgMultGenericT1 = DefKcsgMultGenericT1;
        public float kcsgMultGenericT2 = DefKcsgMultGenericT2;
        public float kcsgMultGenericT3 = DefKcsgMultGenericT3;
        public float kcsgMultGenericT4 = DefKcsgMultGenericT4;

        public float TotalWeight => weightGrow + weightRaid + weightMinorIncident + weightMajorIncident + weightBuildRoad;
        public float TotalSabWeight => weightSabSuccess + weightSabCleanFail + weightSabInjuredFail + weightSabFatalFail;
        public float TotalDisWeight => weightDisSuccess + weightDisCleanFail + weightDisInjuredFail + weightDisFatalFail;

        public float TotalGenWeight => genWeightT1 + genWeightT2 + genWeightT3 + genWeightT4;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tier1Share, "tier1Share", DefTier1Share);
            Scribe_Values.Look(ref tier2Share, "tier2Share", DefTier2Share);
            Scribe_Values.Look(ref tier3Share, "tier3Share", DefTier3Share);
            Scribe_Values.Look(ref tier4Share, "tier4Share", DefTier4Share);
            Scribe_Values.Look(ref weightGrow, "weightGrow", DefWeightGrow);
            Scribe_Values.Look(ref weightRaid, "weightRaid", DefWeightRaid);
            Scribe_Values.Look(ref weightMinorIncident, "weightMinorIncident", DefWeightMinorIncident);
            Scribe_Values.Look(ref weightMajorIncident, "weightMajorIncident", DefWeightMajorIncident);
            Scribe_Values.Look(ref weightBuildRoad, "weightBuildRoad", DefWeightBuildRoad);
            Scribe_Values.Look(ref cooldownGrowDays, "cooldownGrowDays", DefCdGrowDays);
            Scribe_Values.Look(ref cooldownExpandDays, "cooldownExpandDays", DefCdExpandDays);
            Scribe_Values.Look(ref cooldownRaidDays, "cooldownRaidDays", DefCdRaidDays);
            Scribe_Values.Look(ref cooldownBeingRaidedDays, "cooldownBeingRaidedDays", DefCdBeingRaidedDays);

            Scribe_Values.Look(ref maxSettlements, "maxSettlements", DefMaxSettlements);
            Scribe_Values.Look(ref baseGrowthAmount, "baseGrowthAmount", DefBaseGrowth);
            Scribe_Values.Look(ref growthScalingIntensity, "growthScalingIntensity", DefGrowthScaling);
            Scribe_Values.Look(ref expandMinRadius, "expandMinRadius", DefExpandMinRad);
            Scribe_Values.Look(ref expandMaxRadius, "expandMaxRadius", DefExpandMaxRad);
            Scribe_Values.Look(ref maxRoadRange, "maxRoadRange", DefMaxRoadRange);
            Scribe_Values.Look(ref minorIncidentSeverity, "minorIncidentSeverity", DefMinorIncSev);
            Scribe_Values.Look(ref majorIncidentSeverity, "majorIncidentSeverity", DefMajorIncSev);
            Scribe_Values.Look(ref localMaxT1, "localMaxT1", DefLocalMaxT1);
            Scribe_Values.Look(ref localMaxT2, "localMaxT2", DefLocalMaxT2);
            Scribe_Values.Look(ref localMaxT3, "localMaxT3", DefLocalMaxT3);
            Scribe_Values.Look(ref localMaxT4, "localMaxT4", DefLocalMaxT4);
            Scribe_Values.Look(ref expansionSuccessChance, "expansionSuccessChance", DefExpansionSuccessChance);

            Scribe_Values.Look(ref revoltChance, "revoltChance", DefRevoltChance);
            Scribe_Values.Look(ref diplomacyChangeChance, "diplomacyChangeChance", DefDiplomacyChangeChance);
            Scribe_Values.Look(ref enableLeaderHandicap, "enableLeaderHandicap", DefEnableLeaderHandicap);
            Scribe_Values.Look(ref enableUnderdogBuff, "enableUnderdogBuff", DefEnableUnderdogBuff);
            Scribe_Values.Look(ref enableAntiLeaderCoalition, "enableAntiLeaderCoalition", DefEnableAntiLeaderCoalition);
            Scribe_Values.Look(ref enableRandomDiplomacy, "enableRandomDiplomacy", DefEnableRandomDiplomacy);
            Scribe_Values.Look(ref enableExpansionistZeal, "enableExpansionistZeal", DefEnableExpansionistZeal);
            Scribe_Values.Look(ref zealTriggerChance, "zealTriggerChance", DefZealTriggerChance);

            Scribe_Values.Look(ref durLeaderHandicapDays, "durLeaderHandicapDays", DefDurLeaderHandicapDays);
            Scribe_Values.Look(ref cdLeaderHandicapDays, "cdLeaderHandicapDays", DefCdLeaderHandicapDays);
            Scribe_Values.Look(ref durUnderdogBuffDays, "durUnderdogBuffDays", DefDurUnderdogBuffDays);
            Scribe_Values.Look(ref cdUnderdogBuffDays, "cdUnderdogBuffDays", DefCdUnderdogBuffDays);
            Scribe_Values.Look(ref durExpansionistZealDays, "durExpansionistZealDays", DefDurExpansionistZealDays);
            Scribe_Values.Look(ref cdExpansionistZealDays, "cdExpansionistZealDays", DefCdExpansionistZealDays);
            Scribe_Values.Look(ref durAntiLeaderCoalitionDays, "durAntiLeaderCoalitionDays", DefDurAntiLeaderCoalitionDays);
            Scribe_Values.Look(ref cdAntiLeaderCoalitionDays, "cdAntiLeaderCoalitionDays", DefCdAntiLeaderCoalitionDays);

            Scribe_Values.Look(ref playerInfluenceBase, "playerInfluenceBase", DefPlayerInfBase);
            Scribe_Values.Look(ref playerInfluenceWealthRate, "playerInfluenceWealthRate", DefPlayerInfWealthRate);
            Scribe_Values.Look(ref playerInfluenceTimeRate, "playerInfluenceTimeRate", DefPlayerInfTimeRate);
            Scribe_Values.Look(ref minRaidPoints, "minRaidPoints", DefMinRaidPoints);
            Scribe_Values.Look(ref maxRaidPoints, "maxRaidPoints", DefMaxRaidPoints);
            Scribe_Values.Look(ref minRaidPointsMultiplier, "minRaidPointsMultiplier", DefMinRaidPointsMult);
            Scribe_Values.Look(ref maxThreatMultiplier, "maxThreatMultiplier", DefMaxThreatMult);
            Scribe_Values.Look(ref threatDistMult50, "threatDistMult50", DefThreatDist50);
            Scribe_Values.Look(ref threatDistMult75, "threatDistMult75", DefThreatDist75);
            Scribe_Values.Look(ref threatDistMult100, "threatDistMult100", DefThreatDist100);
            Scribe_Values.Look(ref threatTier1Mult, "threatTier1Mult", DefThreatTier1Mult);
            Scribe_Values.Look(ref threatTier2Mult, "threatTier2Mult", DefThreatTier2Mult);
            Scribe_Values.Look(ref threatTier3Mult, "threatTier3Mult", DefThreatTier3Mult);
            Scribe_Values.Look(ref threatTier4Mult, "threatTier4Mult", DefThreatTier4Mult);

            Scribe_Values.Look(ref allowPlayerRaid, "allowPlayerRaid", DefAllowPlayerRaid);
            Scribe_Values.Look(ref allowPlayerOutpostRaid, "allowPlayerOutpostRaid", DefAllowPlayerOutpostRaid);
            Scribe_Values.Look(ref cooldownPlayerRaidDays, "cooldownPlayerRaidDays", DefCdPlayerRaidDays);
            Scribe_Values.Look(ref raidCostPct, "raidCostPct", DefRaidCostPct);
            Scribe_Values.Look(ref raidTargetRadius, "raidTargetRadius", DefRaidTargetRadius);
            Scribe_Values.Look(ref raidAttackerAllyRadius, "raidAttackerAllyRadius", DefRaidAttackerAllyRadius);
            Scribe_Values.Look(ref raidDefenderAllyRadius, "raidDefenderAllyRadius", DefRaidDefenderAllyRadius);
            Scribe_Values.Look(ref minRaidRatio, "minRaidRatio", DefMinRaidRatio);
            Scribe_Values.Look(ref razeChance, "razeChance", DefRazeChance);
            Scribe_Collections.Look(ref raidOutcomes, "raidOutcomes", LookMode.Deep);

            Scribe_Values.Look(ref weightSabSuccess, "weightSabSuccess", DefWeightSabSuccess);
            Scribe_Values.Look(ref weightSabCleanFail, "weightSabCleanFail", DefWeightSabCleanFail);
            Scribe_Values.Look(ref weightSabInjuredFail, "weightSabInjuredFail", DefWeightSabInjuredFail);
            Scribe_Values.Look(ref weightSabFatalFail, "weightSabFatalFail", DefWeightSabFatalFail);
            Scribe_Values.Look(ref sabotageSkillSuccessWeightBonus, "sabotageSkillSuccessWeightBonus", DefSabSkillSuccessWeightBonus);
            Scribe_Values.Look(ref sabotageTierSuccessWeightPenalty, "sabotageTierSuccessWeightPenalty", DefSabTierSuccessWeightPenalty);
            Scribe_Values.Look(ref sabotageHealthImpactWeight, "sabotageHealthImpactWeight", DefSabHealthImpactWeight);
            Scribe_Values.Look(ref sabotageSocialCleanBonus, "sabotageSocialCleanBonus", DefSabSocialCleanBonus);
            Scribe_Values.Look(ref sabotageCombatSurvivalBonus, "sabotageCombatSurvivalBonus", DefSabCombatSurvivalBonus);
            Scribe_Values.Look(ref sabotageBaseReduction, "sabotageBaseReduction", DefSabBaseReduc);
            Scribe_Values.Look(ref sabotageSkillReductionBonus, "sabotageSkillReductionBonus", DefSabSkillReductionBonus);
            Scribe_Values.Look(ref sabotageCooldownDays, "sabotageCooldownDays", DefSabCdDays);

            Scribe_Values.Look(ref weightDisSuccess, "weightDisSuccess", DefWeightDisSuccess);
            Scribe_Values.Look(ref weightDisCleanFail, "weightDisCleanFail", DefWeightDisCleanFail);
            Scribe_Values.Look(ref weightDisInjuredFail, "weightDisInjuredFail", DefWeightDisInjuredFail);
            Scribe_Values.Look(ref weightDisFatalFail, "weightDisFatalFail", DefWeightDisFatalFail);
            Scribe_Values.Look(ref disSkillSuccessWeightBonus, "disSkillSuccessWeightBonus", DefDisSkillSuccessWeightBonus);
            Scribe_Values.Look(ref disTierSuccessWeightPenalty, "disTierSuccessWeightPenalty", DefDisTierSuccessWeightPenalty);
            Scribe_Values.Look(ref disHealthImpactWeight, "disHealthImpactWeight", DefDisHealthImpactWeight);
            Scribe_Values.Look(ref disSocialCleanBonus, "disSocialCleanBonus", DefDisSocialCleanBonus);
            Scribe_Values.Look(ref disCombatSurvivalBonus, "disCombatSurvivalBonus", DefDisCombatSurvivalBonus);
            Scribe_Values.Look(ref disBaseReduction, "disBaseReduction", DefDisBaseReduc);
            Scribe_Values.Look(ref disSkillReductionBonus, "disSkillReductionBonus", DefDisSkillReductionBonus);
            Scribe_Values.Look(ref disCooldownDays, "disCooldownDays", DefDisCdDays);

            Scribe_Values.Look(ref foodLogisticsActive, "foodLogisticsActive", DefFoodLogisticsActive);
            Scribe_Values.Look(ref foodConsumptionPerPawn, "foodConsumptionPerPawn", DefFoodConsumptionPerPawn);
            Scribe_Values.Look(ref foodProductionPerSkill, "foodProductionPerSkill", DefFoodProductionPerSkill);
            Scribe_Values.Look(ref maxLogisticsRange, "maxLogisticsRange", DefMaxLogisticsRange);
            Scribe_Values.Look(ref effAt60, "effAt60", DefEffAt60);
            Scribe_Values.Look(ref effAt80, "effAt80", DefEffAt80);
            Scribe_Values.Look(ref effAt100, "effAt100", DefEffAt100);

            Scribe_Values.Look(ref notifyNewSettlement, "notifyNewSettlement", DefNotifyNewSettlement);
            Scribe_Values.Look(ref notifySettlementRaided, "notifySettlementRaided", DefNotifySettlementRaided);
            Scribe_Values.Look(ref notifyOutpostDestroyed, "notifyOutpostDestroyed", DefNotifyOutpostDestroyed);
            Scribe_Values.Look(ref notifyThreatLevel, "notifyThreatLevel", DefNotifyThreatLevel);
            Scribe_Values.Look(ref notifyDiplomaticChange, "notifyDiplomaticChange", DefNotifyDiplomaticChange);
            Scribe_Values.Look(ref notifyBuffNerf, "notifyBuffNerf", DefNotifyBuffNerf);

            Scribe_Values.Look(ref genWeightT1, "genWeightT1", DefGenWeightT1);
            Scribe_Values.Look(ref genWeightT2, "genWeightT2", DefGenWeightT2);
            Scribe_Values.Look(ref genWeightT3, "genWeightT3", DefGenWeightT3);
            Scribe_Values.Look(ref genWeightT4, "genWeightT4", DefGenWeightT4);

            Scribe_Values.Look(ref kcsgMultTribalT1, "kcsgMultTribalT1", DefKcsgMultTribalT1);
            Scribe_Values.Look(ref kcsgMultTribalT2, "kcsgMultTribalT2", DefKcsgMultTribalT2);
            Scribe_Values.Look(ref kcsgMultTribalT3, "kcsgMultTribalT3", DefKcsgMultTribalT3);
            Scribe_Values.Look(ref kcsgMultTribalT4, "kcsgMultTribalT4", DefKcsgMultTribalT4);
            Scribe_Values.Look(ref kcsgMultGenericT1, "kcsgMultGenericT1", DefKcsgMultGenericT1);
            Scribe_Values.Look(ref kcsgMultGenericT2, "kcsgMultGenericT2", DefKcsgMultGenericT2);
            Scribe_Values.Look(ref kcsgMultGenericT3, "kcsgMultGenericT3", DefKcsgMultGenericT3);
            Scribe_Values.Look(ref kcsgMultGenericT4, "kcsgMultGenericT4", DefKcsgMultGenericT4);

            Scribe_Values.Look(ref initialAllegianceLockDone, "initialAllegianceLockDone", false);
            Scribe_Collections.Look(ref lockedAllegiancePairs, "lockedAllegiancePairs", LookMode.Value);

            if (raidOutcomes == null || raidOutcomes.Count == 0) InitializeDefaults();

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ForceFixedThresholds();
            }
        }

        private void ForceFixedThresholds()
        {
            if (raidOutcomes == null || raidOutcomes.Count != 6)
            {
                ResetRaids();
                return;
            }
            raidOutcomes[0].threshold = 0.00f;
            raidOutcomes[1].threshold = 0.50f;
            raidOutcomes[2].threshold = 1.00f;
            raidOutcomes[3].threshold = 1.50f;
            raidOutcomes[4].threshold = 2.00f;
            raidOutcomes[5].threshold = 3.00f;
        }

        public void InitializeDefaults()
        {
            ResetDailyActions();
            ResetGrowth();
            ResetDiplomacy();
            ResetThreat();
            ResetRaids();
            ResetSabotage();
            ResetDisinformation();
            ResetFoodLogistics();
            ResetNotifications();
            ResetWorldGen();
            ResetGarrisons();
        }

        public void ResetDailyActions()
        {
            tier1Share = DefTier1Share;
            tier2Share = DefTier2Share;
            tier3Share = DefTier3Share;
            tier4Share = DefTier4Share;
            weightGrow = DefWeightGrow;
            weightRaid = DefWeightRaid;
            weightMinorIncident = DefWeightMinorIncident;
            weightMajorIncident = DefWeightMajorIncident;
            weightBuildRoad = DefWeightBuildRoad;
            cooldownGrowDays = DefCdGrowDays;
            cooldownExpandDays = DefCdExpandDays;
            cooldownRaidDays = DefCdRaidDays;
            cooldownBeingRaidedDays = DefCdBeingRaidedDays;
        }

        public void ResetGrowth()
        {
            maxSettlements = DefMaxSettlements;
            baseGrowthAmount = DefBaseGrowth;
            growthScalingIntensity = DefGrowthScaling;
            expandMinRadius = DefExpandMinRad;
            expandMaxRadius = DefExpandMaxRad;
            maxRoadRange = DefMaxRoadRange;
            minorIncidentSeverity = DefMinorIncSev;
            majorIncidentSeverity = DefMajorIncSev;
            localMaxT1 = DefLocalMaxT1;
            localMaxT2 = DefLocalMaxT2;
            localMaxT3 = DefLocalMaxT3;
            localMaxT4 = DefLocalMaxT4;
            expansionSuccessChance = DefExpansionSuccessChance;
        }

        public void ResetDiplomacy()
        {
            enableLeaderHandicap = DefEnableLeaderHandicap;
            enableUnderdogBuff = DefEnableUnderdogBuff;
            enableAntiLeaderCoalition = DefEnableAntiLeaderCoalition;
            enableRandomDiplomacy = DefEnableRandomDiplomacy;
            enableExpansionistZeal = DefEnableExpansionistZeal;
            diplomacyChangeChance = DefDiplomacyChangeChance;
            revoltChance = DefRevoltChance;
            zealTriggerChance = DefZealTriggerChance;

            durLeaderHandicapDays = DefDurLeaderHandicapDays;
            cdLeaderHandicapDays = DefCdLeaderHandicapDays;
            durUnderdogBuffDays = DefDurUnderdogBuffDays;
            cdUnderdogBuffDays = DefCdUnderdogBuffDays;
            durExpansionistZealDays = DefDurExpansionistZealDays;
            cdExpansionistZealDays = DefCdExpansionistZealDays;
            durAntiLeaderCoalitionDays = DefDurAntiLeaderCoalitionDays;
            cdAntiLeaderCoalitionDays = DefCdAntiLeaderCoalitionDays;
        }

        public void ResetGarrisons()
        {
            kcsgMultTribalT1 = DefKcsgMultTribalT1;
            kcsgMultTribalT2 = DefKcsgMultTribalT2;
            kcsgMultTribalT3 = DefKcsgMultTribalT3;
            kcsgMultTribalT4 = DefKcsgMultTribalT4;
            kcsgMultGenericT1 = DefKcsgMultGenericT1;
            kcsgMultGenericT2 = DefKcsgMultGenericT2;
            kcsgMultGenericT3 = DefKcsgMultGenericT3;
            kcsgMultGenericT4 = DefKcsgMultGenericT4;
        }
        public void ResetThreat()
        {
            playerInfluenceBase = DefPlayerInfBase;
            playerInfluenceWealthRate = DefPlayerInfWealthRate;
            playerInfluenceTimeRate = DefPlayerInfTimeRate;
            minRaidPoints = DefMinRaidPoints;
            maxRaidPoints = DefMaxRaidPoints;
            minRaidPointsMultiplier = DefMinRaidPointsMult;
            maxThreatMultiplier = DefMaxThreatMult;
            threatDistMult50 = DefThreatDist50;
            threatDistMult75 = DefThreatDist75;
            threatDistMult100 = DefThreatDist100;
            threatTier1Mult = DefThreatTier1Mult;
            threatTier2Mult = DefThreatTier2Mult;
            threatTier3Mult = DefThreatTier3Mult;
            threatTier4Mult = DefThreatTier4Mult;
        }

        public void ResetRaids()
        {
            allowPlayerRaid = DefAllowPlayerRaid;
            allowPlayerOutpostRaid = DefAllowPlayerOutpostRaid;
            cooldownPlayerRaidDays = DefCdPlayerRaidDays;
            raidCostPct = DefRaidCostPct;
            raidTargetRadius = DefRaidTargetRadius;
            raidAttackerAllyRadius = DefRaidAttackerAllyRadius;
            raidDefenderAllyRadius = DefRaidDefenderAllyRadius;
            minRaidRatio = DefMinRaidRatio;
            razeChance = DefRazeChance;

            raidOutcomes = new List<RaidOutcome>
            {
                new RaidOutcome { threshold = 0.00f, winChance = 0.00f, attLossWin = 0.50f, defLossLoss = 0.05f, attLossLoss = 0.50f, defLossWin = 0.01f },
                new RaidOutcome { threshold = 0.50f, winChance = 0.15f, attLossWin = 0.35f, defLossLoss = 0.10f, attLossLoss = 0.45f, defLossWin = 0.05f },
                new RaidOutcome { threshold = 1.00f, winChance = 0.30f, attLossWin = 0.25f, defLossLoss = 0.20f, attLossLoss = 0.35f, defLossWin = 0.10f },
                new RaidOutcome { threshold = 1.50f, winChance = 0.55f, attLossWin = 0.20f, defLossLoss = 0.25f, attLossLoss = 0.30f, defLossWin = 0.15f },
                new RaidOutcome { threshold = 2.00f, winChance = 0.80f, attLossWin = 0.15f, defLossLoss = 0.30f, attLossLoss = 0.25f, defLossWin = 0.15f },
                new RaidOutcome { threshold = 3.00f, winChance = 1.00f, attLossWin = 0.05f, defLossLoss = 0.50f, attLossLoss = 0.20f, defLossWin = 0.20f }
            };
        }

        public void ResetSabotage()
        {
            weightSabSuccess = DefWeightSabSuccess;
            weightSabCleanFail = DefWeightSabCleanFail;
            weightSabInjuredFail = DefWeightSabInjuredFail;
            weightSabFatalFail = DefWeightSabFatalFail;
            sabotageSkillSuccessWeightBonus = DefSabSkillSuccessWeightBonus;
            sabotageTierSuccessWeightPenalty = DefSabTierSuccessWeightPenalty;
            sabotageHealthImpactWeight = DefSabHealthImpactWeight;
            sabotageSocialCleanBonus = DefSabSocialCleanBonus;
            sabotageCombatSurvivalBonus = DefSabCombatSurvivalBonus;
            sabotageBaseReduction = DefSabBaseReduc;
            sabotageSkillReductionBonus = DefSabSkillReductionBonus;
            sabotageCooldownDays = DefSabCdDays;
        }

        public void ResetDisinformation()
        {
            weightDisSuccess = DefWeightDisSuccess;
            weightDisCleanFail = DefWeightDisCleanFail;
            weightDisInjuredFail = DefWeightDisInjuredFail;
            weightDisFatalFail = DefWeightDisFatalFail;
            disSkillSuccessWeightBonus = DefDisSkillSuccessWeightBonus;
            disTierSuccessWeightPenalty = DefDisTierSuccessWeightPenalty;
            disHealthImpactWeight = DefDisHealthImpactWeight;
            disSocialCleanBonus = DefDisSocialCleanBonus;
            disCombatSurvivalBonus = DefDisCombatSurvivalBonus;
            disBaseReduction = DefDisBaseReduc;
            disSkillReductionBonus = DefDisSkillReductionBonus;
            disCooldownDays = DefDisCdDays;
        }

        public void ResetFoodLogistics()
        {
            foodLogisticsActive = DefFoodLogisticsActive;
            foodConsumptionPerPawn = DefFoodConsumptionPerPawn;
            foodProductionPerSkill = DefFoodProductionPerSkill;
            maxLogisticsRange = DefMaxLogisticsRange;
            effAt60 = DefEffAt60;
            effAt80 = DefEffAt80;
            effAt100 = DefEffAt100;
        }

        public void ResetNotifications()
        {
            notifyNewSettlement = DefNotifyNewSettlement;
            notifySettlementRaided = DefNotifySettlementRaided;
            notifyOutpostDestroyed = DefNotifyOutpostDestroyed;
            notifyThreatLevel = DefNotifyThreatLevel;
            notifyDiplomaticChange = DefNotifyDiplomaticChange;
            notifyBuffNerf = DefNotifyBuffNerf;
        }

        public void ResetWorldGen()
        {
            genWeightT1 = DefGenWeightT1;
            genWeightT2 = DefGenWeightT2;
            genWeightT3 = DefGenWeightT3;
            genWeightT4 = DefGenWeightT4;
        }

        public bool IsPairLocked(Faction a, Faction b)
        {
            if (a == null || b == null) return false;
            string key = GetFactionPairKey(a, b);
            return lockedAllegiancePairs.Contains(key);
        }

        public string GetFactionPairKey(Faction a, Faction b)
        {
            // We sort the IDs so that (A, B) and (B, A) always generate the same key
            return a.loadID < b.loadID ? $"{a.loadID}_{b.loadID}" : $"{b.loadID}_{a.loadID}";
        }

        public void ResetAllegianceLocks(bool lockAllHostiles = false)
        {
            // Clear existing locks - always safe
            lockedAllegiancePairs = new HashSet<string>();

            // SAFETY: Prevent null reference if called from Main Menu
            if (Current.ProgramState != ProgramState.Playing) return;

            Faction player = Faction.OfPlayer;
            if (player == null) return;

            var factions = Find.FactionManager.AllFactionsListForReading
                .Where(f => !f.def.hidden && !f.defeated).ToList();

            foreach (var f in factions)
            {
                // 1. Identify "Insect/Hive" status vs "Standard Permanent Enemy"
                bool isHive = f.def.defName.Contains("Insect") || f.Name.Contains("Hive");
                bool isVanillaPermEnemy = f.def.permanentEnemy;
                bool isAnyPermType = isHive || isVanillaPermEnemy;

                // 2. PLAYER LOCK: If it's a permanent enemy type, lock it against the player
                if (isAnyPermType)
                {
                    lockedAllegiancePairs.Add(GetFactionPairKey(player, f));
                }

                // 3. NPC-NPC LOCK
                foreach (var other in factions)
                {
                    if (f == other || other.IsPlayer) continue;

                    bool otherIsHive = other.def.defName.Contains("Insect") || other.Name.Contains("Hive");
                    bool otherIsVanillaPerm = other.def.permanentEnemy;

                    // --- HARD LOCK: Hives/Insects are locked against EVERYBODY, always ---
                    if (isHive || otherIsHive)
                    {
                        lockedAllegiancePairs.Add(GetFactionPairKey(f, other));
                        continue;
                    }

                    // --- CONDITIONAL LOCKS: For non-hive factions ---
                    if (lockAllHostiles)
                    {
                        // Lock if either is a vanilla permanent enemy type and they are hostile
                        if ((isVanillaPermEnemy && f.HostileTo(other)) || (otherIsVanillaPerm && other.HostileTo(f)))
                        {
                            lockedAllegiancePairs.Add(GetFactionPairKey(f, other));
                        }
                    }
                    else if (isVanillaPermEnemy && otherIsVanillaPerm && f.HostileTo(other))
                    {
                        // Standard behavior: only lock if BOTH are vanilla permanent enemies
                        lockedAllegiancePairs.Add(GetFactionPairKey(f, other));
                    }
                }
            }
        }

        public void EnsureInitialLaunchDefaults()
        {
            if (!initialAllegianceLockDone)
            {
                ResetAllegianceLocks();
                initialAllegianceLockDone = true;
            }
        }
    }
}