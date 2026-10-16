using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Outposts;
using HarmonyLib;
using TSA_WorldDomination.Settings;

namespace TSA_WorldDomination
{
    public enum LogisticsMode
    {
        Manual = 0,
        /// <summary>Legacy save/UI value — normalized to <see cref="Smart"/> on load.</summary>
        Equal = 1,
        /// <summary>Legacy save/UI value — normalized to <see cref="Smart"/> on load.</summary>
        FullyFeed = 2,
        /// <summary>Multi-hub deficit cover, Keep, water-fill.</summary>
        Smart = 3,
        /// <summary>Legacy scribe name for <see cref="Smart"/> (same int).</summary>
        BiggestDeficit = Smart,
    }

    public static class LogisticsModeUtil
    {
        public static bool UsesAutomaticAssignment(LogisticsMode mode) => mode != LogisticsMode.Manual;

        public static bool IsSmartMode(LogisticsMode mode) =>
            mode == LogisticsMode.Smart
            || mode == LogisticsMode.BiggestDeficit
            || mode == LogisticsMode.Equal
            || mode == LogisticsMode.FullyFeed;

        public static LogisticsMode Normalize(LogisticsMode mode)
        {
            if (mode == LogisticsMode.Equal || mode == LogisticsMode.FullyFeed || mode == LogisticsMode.BiggestDeficit)
                return LogisticsMode.Smart;
            return mode == LogisticsMode.Manual ? LogisticsMode.Manual : LogisticsMode.Smart;
        }

        public static string InspectModeLabelKey(LogisticsMode mode)
        {
            mode = Normalize(mode);
            return mode == LogisticsMode.Manual ? null : "TSA_LogisticsMode_" + mode;
        }
    }

    [StaticConstructorOnStartup]
    public static class LogisticsResources
    {
        public static readonly Material LogisticsLine =
            MaterialPool.MatFrom("UI/Overlays/ThingLine", ShaderDatabase.WorldOverlayTransparent, Color.white, 3590);

        public static readonly FieldInfo TicksTillProductionField =
            typeof(Outpost).GetField("ticksTillProduction", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        public static readonly FieldInfo OccupantsField =
            typeof(Outpost).GetField("occupants", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
    }

    public class CompProperties_OutpostLogistics : WorldObjectCompProperties
    {
        public CompProperties_OutpostLogistics() => this.compClass = typeof(CompOutpostLogistics);
    }

    public class CompOutpostLogistics : WorldObjectComp
    {
        public float currentFood = 50f;
        public const float MaxFood = 100f;
        public float lastNetChange = 0f;
        public LogisticsMode mode = LogisticsMode.Manual;
        public bool produceForLogistics = false;

        private string cachedInspectString;
        private int cachedInspectTick = -999;
        private const int InspectCacheInterval = 60;

        public override void Initialize(WorldObjectCompProperties props)
        {
            base.Initialize(props);
            if (WorldComponent_LogisticsManager.IsFoodProducerOutpost(parent))
            {
                this.mode = LogisticsMode.Smart;
                this.produceForLogistics = true;
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref currentFood, "currentFood", 50f);
            Scribe_Values.Look(ref lastNetChange, "lastNetChange", 0f);
            Scribe_Values.Look(ref mode, "mode", LogisticsMode.Manual);
            Scribe_Values.Look(ref produceForLogistics, "produceForLogistics", false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                mode = LogisticsModeUtil.Normalize(mode);
        }

        public override string CompInspectStringExtra()
        {
            if (!WorldDominationMod.settings.foodLogisticsActive) return null;

            int tick = Find.TickManager?.TicksGame ?? 0;
            if (cachedInspectString != null && tick - cachedInspectTick < InspectCacheInterval)
                return cachedInspectString;

            float net = lastNetChange;
            var manager = Find.World?.GetComponent<WorldComponent_LogisticsManager>();
            if (manager != null && parent != null)
                net = manager.GetLogisticsNetDailyForOutpost(parent);

            string balanceStr = net >= 0 ? $"+{net:F1}" : $"{net:F1}";
            Color col = net > 0.1f ? Color.green : (net < -0.1f ? Color.red : Color.yellow);
            string modeKey = LogisticsModeUtil.InspectModeLabelKey(mode);
            string modeStr = modeKey == null ? "" : $" [{modeKey.Translate()}]";

            string baseStr = "TSA_FoodSupplyInspect".Translate(
                currentFood.ToString("F1"),
                MaxFood.ToString("F0"),
                balanceStr.Colorize(col)
            ) + modeStr;

            if (produceForLogistics)
                baseStr += "\n" + "TSA_LogisticsActive".Translate().Colorize(Color.cyan);

            cachedInspectString = baseStr;
            cachedInspectTick = tick;
            return baseStr;
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();

            if (!(parent is Outpost outpost)) return;
            var manager = Find.World.GetComponent<WorldComponent_LogisticsManager>();
            if (manager == null) return;
            if (!Find.WorldSelector.IsSelected(outpost)) return;

            bool isProducer = WorldComponent_LogisticsManager.IsFoodProducerOutpost(outpost);
            if (isProducer)
                GenDraw.DrawWorldRadiusRing(outpost.Tile, WorldDominationMod.settings.maxLogisticsRange);

            manager.DrawSelectionLogisticsLines(outpost.Tile);
        }

        public void ConvertInventoryToVirtualFood()
        {
            if (!(parent is Outpost outpost)) return;

            var inventory = outpost.Things;
            if (inventory == null || !inventory.Any())
            {
                Messages.Message("TSA_WD_NoFoodInInventory".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }

            float foodGained = 0f;
            List<Thing> toDestroy = new List<Thing>();

            foreach (Thing t in inventory)
            {
                float spaceLeft = MaxFood - (currentFood + foodGained);
                if (spaceLeft < 0.01f) break;

                if (t.def.ingestible == null) continue;
                float nutritionValue = t.def.GetStatValueAbstract(StatDefOf.Nutrition);
                if (nutritionValue <= 0) continue;

                int countToConsume = Mathf.Min(t.stackCount, Mathf.FloorToInt(spaceLeft / nutritionValue));

                if (countToConsume > 0)
                {
                    float totalValue = countToConsume * nutritionValue;
                    if (totalValue >= 0.01f)
                    {
                        foodGained += totalValue;
                        if (countToConsume == t.stackCount)
                            toDestroy.Add(t);
                        else
                            t.SplitOff(countToConsume).Destroy();
                    }
                }
            }

            foreach (Thing t in toDestroy)
                t.Destroy();

            if (foodGained > 0)
            {
                currentFood = Mathf.Min(currentFood + foodGained, MaxFood);
                Messages.Message("TSA_WD_FoodConverted".Translate(foodGained.ToString("F1")), MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Messages.Message("TSA_WD_NoValidFood".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }
    }

    [HarmonyPatch(typeof(Outpost), nameof(Outpost.GetGizmos))]
    public static class Patch_OutpostLogisticsGizmos
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Outpost __instance)
        {
            if (__result != null)
            {
                foreach (var g in __result) yield return g;
            }

            if (__instance.Faction != Faction.OfPlayer) yield break;
            if (!WorldDominationMod.settings.foodLogisticsActive) yield break;

            var logiComp = __instance.GetComponent<CompOutpostLogistics>();
            if (logiComp == null) yield break;

            if (WorldComponent_LogisticsManager.IsFoodProducerOutpost(__instance))
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "TSA_ProduceForLogistics".Translate(),
                    defaultDesc = "TSA_ProduceForLogisticsDesc".Translate(),
                    icon = logiComp.produceForLogistics
                        ? ContentFinder<Texture2D>.Get("UI/Commands/LogisticsMode")
                        : ContentFinder<Texture2D>.Get("UI/Commands/LogisticsMode_Off"),
                    isActive = () => logiComp.produceForLogistics,
                    toggleAction = () =>
                    {
                        logiComp.produceForLogistics = !logiComp.produceForLogistics;
                        var manager = Find.World.GetComponent<WorldComponent_LogisticsManager>();
                        manager?.NotifyFoodLogisticsInputsChanged();
                        manager?.CompleteSmartLogisticsRefreshNow();
                    }
                };
            }

            if (logiComp.currentFood < CompOutpostLogistics.MaxFood)
            {
                yield return new Command_Action
                {
                    defaultLabel = "TSA_WD_ConvertFood".Translate(),
                    defaultDesc = "TSA_WD_ConvertFoodDesc".Translate(),
                    icon = ContentFinder<Texture2D>.Get("UI/Commands/ConvertFood"),
                    action = delegate { logiComp.ConvertInventoryToVirtualFood(); }
                };
            }
        }
    }

    [HarmonyPatch(typeof(Outpost), nameof(Outpost.ProductionString))]
    public static class Patch_Outpost_ProductionString
    {
        public static bool Prefix(Outpost __instance, ref string __result)
        {
            if (!WorldDominationMod.settings.foodLogisticsActive) return true;

            var logi = __instance.GetComponent<CompOutpostLogistics>();
            if (logi != null && logi.produceForLogistics)
            {
                __result = "TSA_LogisticsProductionFrozen".Translate().Colorize(Color.yellow);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Outpost), nameof(Outpost.TickInterval))]
    public static class Patch_Outpost_ProductionFreeze
    {
        public static void Prefix(Outpost __instance, ref int delta)
        {
            if (!WorldDominationMod.settings.foodLogisticsActive) return;

            var logi = __instance.GetComponent<CompOutpostLogistics>();
            if (logi != null && logi.produceForLogistics && LogisticsResources.TicksTillProductionField != null)
            {
                int currentTimer = (int)LogisticsResources.TicksTillProductionField.GetValue(__instance);
                LogisticsResources.TicksTillProductionField.SetValue(__instance, currentTimer + delta);
            }
        }
    }

    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.SpawnSetup))]
    public static class Patch_WorldObject_LogisticsSpawnSetup
    {
        public static void Postfix(WorldObject __instance)
        {
            if (__instance?.GetComponent<CompOutpostLogistics>() == null) return;
            Faction player = Faction.OfPlayerSilentFail;
            if (player == null || __instance.Faction != player) return;
            Find.World?.GetComponent<WorldComponent_LogisticsManager>()?.NotifyLogisticsTopologyChanged();
        }
    }

    [HarmonyPatch(typeof(WorldObject), nameof(WorldObject.PostRemove))]
    public static class Patch_WorldObject_LogisticsPostRemove
    {
        public static void Postfix(WorldObject __instance)
        {
            if (__instance?.GetComponent<CompOutpostLogistics>() == null) return;
            Find.World?.GetComponent<WorldComponent_LogisticsManager>()?.NotifyLogisticsTopologyChanged();
        }
    }

    public class WorldComponent_LogisticsManager : WorldComponent
    {
        public class LogisticsLink : IExposable
        {
            public int sourceTile;
            public int destTile;
            public float manualAssignment;
            public void ExposeData()
            {
                Scribe_Values.Look(ref sourceTile, "sourceTile");
                Scribe_Values.Look(ref destTile, "destTile");
                Scribe_Values.Look(ref manualAssignment, "manualAssignment");
            }
        }

        private struct LogisticsCacheEntry
        {
            public float DailyProduction;
            public float DailyDemand;
            public float NetLogistics;
        }

        public List<LogisticsLink> manualLinks = new List<LogisticsLink>();
        private int nextTick = -1;
        private const int CheckInterval = 6000;

        private readonly List<(WorldObject Obj, CompOutpostLogistics Logi)> playerLogisticsNodesOrdered =
            new List<(WorldObject, CompOutpostLogistics)>(64);
        private bool playerLogisticsRegistryDirty = true;

        private int manualLinksMutationVersion;
        private int manualLinkAggBuiltVersion = -1;
        private int logisticsCacheGeneration;
        private int logisticsCacheBuiltGeneration = -1;
        private Dictionary<int, float> manualOutgoingSumBySourceTile;
        private Dictionary<int, float> manualIncomingWeightedSumByDestTile;
        private Dictionary<int, List<int>> manualOutgoingDestsBySourceTile;
        private Dictionary<int, List<int>> manualIncomingSourcesByDestTile;
        private Dictionary<int, LogisticsCacheEntry> logisticsCacheByObjectId;

        private int manualLinkNotifySuppressDepth;
        private bool manualLinkNotifyPendingAfterSuppress;

        private bool smartLogisticsDirty = true;
        private bool smartDirtyFromExplicitNotify;
        private long lastSmartCompletedSoftFingerprint = long.MinValue;

        private readonly Dictionary<CompOutpostLogistics, float> pulsePendingChanges =
            new Dictionary<CompOutpostLogistics, float>(64);

        public int LogisticsNetDisplayGeneration => logisticsCacheGeneration;

        public WorldComponent_LogisticsManager(World world) : base(world) { }

        public static bool IsFoodProducerOutpost(WorldObject obj) =>
            obj?.def?.defName != null
            && (obj.def.defName.Contains("Farming") || obj.def.defName.Contains("Hunting"));

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref manualLinks, "manualLinks", LookMode.Deep);
            if (manualLinks == null) manualLinks = new List<LogisticsLink>();
        }

        public override void FinalizeInit(bool fromLoad)
        {
            base.FinalizeInit(fromLoad);
            // Dirty only — do not rebuild/reconcile here. Faction.OfPlayer is often unset during world gen / early load
            // (same as WD2: NotifyLogisticsTopologyChanged marks dirty; rebuild runs on first gameplay access).
            NotifyLogisticsTopologyChanged();
        }

        public override void WorldComponentTick()
        {
            var s = WorldDominationMod.settings;
            if (s == null || !s.foodLogisticsActive) return;

            TickFoodSettingsSnapshot();

            if (Find.TickManager.TicksGame >= nextTick || nextTick == -1)
            {
                RunLogisticsPulse();
                nextTick = Find.TickManager.TicksGame + CheckInterval;
            }
        }

        private int lastSettingsFingerprint = int.MinValue;

        private void TickFoodSettingsSnapshot()
        {
            var s = WorldDominationMod.settings;
            if (s == null) return;
            int fp = unchecked(
                s.maxLogisticsRange
                ^ (Mathf.RoundToInt(s.foodConsumptionPerPawn * 100f) << 8)
                ^ (Mathf.RoundToInt(s.foodProductionPerSkill * 100f) << 16)
                ^ (s.foodLogisticsActive ? 1 : 0));
            if (lastSettingsFingerprint == int.MinValue)
            {
                lastSettingsFingerprint = fp;
                return;
            }
            if (fp != lastSettingsFingerprint)
            {
                lastSettingsFingerprint = fp;
                NotifyFoodLogisticsInputsChanged();
            }
        }

        public void NotifyFoodSettingsChanged()
        {
            lastSettingsFingerprint = int.MinValue;
            NotifyFoodLogisticsInputsChanged();
        }

        public float GetEfficiency(int start, int end)
        {
            var s = WorldDominationMod.settings;
            float dist = Find.WorldGrid.ApproxDistanceInTiles(start, end);
            return dist <= (float)s.maxLogisticsRange ? 1f : 0f;
        }

        public float GetDailyProduction(WorldObject obj)
        {
            if (obj == null) return 0f;
            EnsureLogisticsCacheFresh();
            if (logisticsCacheByObjectId != null && logisticsCacheByObjectId.TryGetValue(obj.ID, out LogisticsCacheEntry e))
                return e.DailyProduction;
            return ComputeDailyProductionUncached(obj);
        }

        public float GetDailyDemand(WorldObject obj)
        {
            if (obj == null) return 0f;
            EnsureLogisticsCacheFresh();
            if (logisticsCacheByObjectId != null && logisticsCacheByObjectId.TryGetValue(obj.ID, out LogisticsCacheEntry e))
                return e.DailyDemand;
            return ComputeDailyDemandUncached(obj);
        }

        public float GetLogisticsNetDailyForOutpost(WorldObject target)
        {
            if (target == null) return 0f;
            EnsureManualLinkAggregatesFresh();
            EnsureLogisticsCacheFresh();
            if (logisticsCacheByObjectId != null && logisticsCacheByObjectId.TryGetValue(target.ID, out LogisticsCacheEntry e))
                return e.NetLogistics;
            float incoming = GetIncomingManualWeightedSumForTile(target.Tile);
            float outgoing = GetOutgoingManualSumForTile(target.Tile);
            return GetDailyProduction(target) - GetDailyDemand(target) + incoming - outgoing;
        }

        public float GetOutgoingManualSumForTile(int tile)
        {
            EnsureManualLinkAggregatesFresh();
            if (manualOutgoingSumBySourceTile != null && manualOutgoingSumBySourceTile.TryGetValue(tile, out float v))
                return v;
            return 0f;
        }

        public float GetIncomingManualWeightedSumForTile(int tile)
        {
            EnsureManualLinkAggregatesFresh();
            if (manualIncomingWeightedSumByDestTile != null && manualIncomingWeightedSumByDestTile.TryGetValue(tile, out float v))
                return v;
            return 0f;
        }

        /// <summary>Surplus available to distribute from a hub tile. 0 if not a food producer or produceForLogistics is off.</summary>
        public float GetDistributionBudgetForSourceTile(int sourceTile)
        {
            RebuildPlayerLogisticsRegistryIfNeeded();
            for (int i = 0; i < playerLogisticsNodesOrdered.Count; i++)
            {
                var node = playerLogisticsNodesOrdered[i];
                if (node.Obj.Tile != sourceTile) continue;
                if (!IsFoodProducerOutpost(node.Obj) || !node.Logi.produceForLogistics)
                    return 0f;
                return Mathf.Max(0f, GetDailyProduction(node.Obj) - GetDailyDemand(node.Obj));
            }
            return 0f;
        }

        public float GetOutgoingManualSumForDirectedLink(int sourceTile, int destTile)
        {
            float sum = 0f;
            for (int i = 0; i < manualLinks.Count; i++)
            {
                LogisticsLink l = manualLinks[i];
                if (l.sourceTile == sourceTile && l.destTile == destTile)
                    sum += l.manualAssignment;
            }
            return sum;
        }

        public void MergeDuplicateManualLinksFromSource(int sourceTile)
        {
            var byDest = new Dictionary<int, List<LogisticsLink>>();
            for (int i = 0; i < manualLinks.Count; i++)
            {
                var l = manualLinks[i];
                if (l.sourceTile != sourceTile) continue;
                if (!byDest.TryGetValue(l.destTile, out var list))
                {
                    list = new List<LogisticsLink>(2);
                    byDest[l.destTile] = list;
                }
                list.Add(l);
            }
            bool changed = false;
            foreach (var kv in byDest)
            {
                var list = kv.Value;
                if (list.Count < 2) continue;
                float sum = 0f;
                for (int i = 0; i < list.Count; i++)
                    sum += list[i].manualAssignment;
                list[0].manualAssignment = sum;
                for (int i = 1; i < list.Count; i++)
                    manualLinks.Remove(list[i]);
                changed = true;
            }
            if (changed)
                NotifyManualLinksChanged();
        }

        public bool ClampManualAssignmentsForSourceBudget(int sourceTile)
        {
            MergeDuplicateManualLinksFromSource(sourceTile);
            float budget = GetDistributionBudgetForSourceTile(sourceTile);
            float total = GetOutgoingManualSumForTile(sourceTile);
            if (total <= budget + 0.001f) return false;

            float remaining = budget;
            bool changed = false;
            for (int i = 0; i < manualLinks.Count; i++)
            {
                LogisticsLink link = manualLinks[i];
                if (link.sourceTile != sourceTile || link.manualAssignment <= 0f) continue;

                float clamped = Mathf.Min(link.manualAssignment, remaining);
                clamped = (float)Math.Round(clamped, 1);
                if (clamped > remaining) clamped = remaining;
                if (Mathf.Abs(link.manualAssignment - clamped) > 0.0001f)
                {
                    link.manualAssignment = Mathf.Max(0f, clamped);
                    changed = true;
                }
                remaining = Mathf.Max(0f, remaining - link.manualAssignment);
            }
            if (changed)
                NotifyManualLinksChanged();
            return changed;
        }

        private float ComputeDailyProductionUncached(WorldObject obj)
        {
            var s = WorldDominationMod.settings;
            if (!(obj is Outpost op)) return s.foodConsumptionPerPawn * 2f;

            float production = s.foodConsumptionPerPawn * 2f;

            if (IsFoodProducerOutpost(op))
            {
                float totalSkill = 0f;
                foreach (Pawn pawn in op.AllPawns)
                {
                    if (pawn.skills == null) continue;
                    if (op.def.defName.Contains("Farming"))
                        totalSkill += pawn.skills.GetSkill(SkillDefOf.Plants).Level;
                    else
                        totalSkill += Mathf.Max(pawn.skills.GetSkill(SkillDefOf.Shooting).Level, pawn.skills.GetSkill(SkillDefOf.Animals).Level);
                }
                production += Mathf.Min(totalSkill * s.foodProductionPerSkill, 35.0f);
            }

            return production;
        }

        private float ComputeDailyDemandUncached(WorldObject obj)
        {
            var s = WorldDominationMod.settings;
            float count = 0f;
            if (obj is MapParent mapParent && mapParent.HasMap) count = (float)mapParent.Map.mapPawns.AllPawnsCount;
            else if (obj is Outpost outpost) count = (float)outpost.AllPawns.Count();
            return count * s.foodConsumptionPerPawn;
        }

        private void BumpManualLinkStructureVersionImmediate()
        {
            manualLinksMutationVersion++;
            logisticsCacheGeneration++;
            manualLinkAggBuiltVersion = -1;
            logisticsCacheBuiltGeneration = -1;
        }

        public void NotifyManualLinksChanged()
        {
            if (manualLinkNotifySuppressDepth > 0)
            {
                manualLinkNotifyPendingAfterSuppress = true;
                manualLinkAggBuiltVersion = -1;
                logisticsCacheBuiltGeneration = -1;
                return;
            }
            BumpManualLinkStructureVersionImmediate();
        }

        public void InvalidateLogisticsNumericCaches()
        {
            manualLinkAggBuiltVersion = -1;
            logisticsCacheBuiltGeneration = -1;
        }

        public void BeginManualLinkNotifySuppress() => manualLinkNotifySuppressDepth++;

        public void EndManualLinkNotifySuppress()
        {
            manualLinkNotifySuppressDepth--;
            if (manualLinkNotifySuppressDepth < 0) manualLinkNotifySuppressDepth = 0;
            if (manualLinkNotifySuppressDepth == 0 && manualLinkNotifyPendingAfterSuppress)
            {
                manualLinkNotifyPendingAfterSuppress = false;
                BumpManualLinkStructureVersionImmediate();
            }
        }

        public void BumpLogisticsDataVersion() => logisticsCacheGeneration++;

        public void NotifyFoodLogisticsInputsChanged()
        {
            BumpLogisticsDataVersion();
            NotifySmartLogisticsDirty();
        }

        public void NotifyLogisticsTopologyChanged()
        {
            // WD2 pattern: mark dirty only. Eager rebuild here races world gen (no player faction yet).
            playerLogisticsRegistryDirty = true;
            logisticsCacheGeneration++;
            NotifySmartLogisticsDirty();
        }

        public void NotifySmartLogisticsDirty()
        {
            smartLogisticsDirty = true;
            smartDirtyFromExplicitNotify = true;
        }

        private void PruneOrphanManualLinksAfterRebuild()
        {
            var live = new HashSet<int>();
            for (int i = 0; i < playerLogisticsNodesOrdered.Count; i++)
                live.Add(playerLogisticsNodesOrdered[i].Obj.Tile);
            int removed = manualLinks.RemoveAll(l => !live.Contains(l.sourceTile) || !live.Contains(l.destTile));
            if (removed > 0)
                NotifyManualLinksChanged();
        }

        private bool RebuildPlayerLogisticsRegistryIfNeeded()
        {
            if (!playerLogisticsRegistryDirty) return false;

            Faction player = Faction.OfPlayerSilentFail;
            if (player == null)
            {
                // Keep dirty so we rebuild once gameplay has a player faction.
                return false;
            }

            playerLogisticsNodesOrdered.Clear();
            if (Find.WorldObjects == null)
            {
                playerLogisticsRegistryDirty = false;
                return true;
            }
            var all = Find.WorldObjects.AllWorldObjects;
            for (int i = 0; i < all.Count; i++)
            {
                var wo = all[i];
                if (wo == null || wo.Destroyed || wo.Faction != player) continue;
                var logi = wo.GetComponent<CompOutpostLogistics>();
                if (logi == null) continue;
                logi.mode = LogisticsModeUtil.Normalize(logi.mode);
                playerLogisticsNodesOrdered.Add((wo, logi));
            }
            playerLogisticsNodesOrdered.Sort((a, b) => a.Obj.ID.CompareTo(b.Obj.ID));
            playerLogisticsRegistryDirty = false;
            PruneOrphanManualLinksAfterRebuild();
            return true;
        }

        public IReadOnlyList<(WorldObject Obj, CompOutpostLogistics Logi)> GetCachedPlayerLogisticsNodes()
        {
            RebuildPlayerLogisticsRegistryIfNeeded();
            return playerLogisticsNodesOrdered;
        }

        private void EnsureManualLinkAggregatesFresh()
        {
            if (manualLinkAggBuiltVersion == manualLinksMutationVersion && manualOutgoingSumBySourceTile != null)
                return;
            manualLinkAggBuiltVersion = manualLinksMutationVersion;
            manualOutgoingSumBySourceTile ??= new Dictionary<int, float>();
            manualOutgoingSumBySourceTile.Clear();
            manualIncomingWeightedSumByDestTile ??= new Dictionary<int, float>();
            manualIncomingWeightedSumByDestTile.Clear();
            manualOutgoingDestsBySourceTile ??= new Dictionary<int, List<int>>();
            ClearDrawIndexLists(manualOutgoingDestsBySourceTile);
            manualIncomingSourcesByDestTile ??= new Dictionary<int, List<int>>();
            ClearDrawIndexLists(manualIncomingSourcesByDestTile);

            for (int i = 0; i < manualLinks.Count; i++)
            {
                LogisticsLink l = manualLinks[i];
                if (manualOutgoingSumBySourceTile.TryGetValue(l.sourceTile, out float o))
                    manualOutgoingSumBySourceTile[l.sourceTile] = o + l.manualAssignment;
                else
                    manualOutgoingSumBySourceTile[l.sourceTile] = l.manualAssignment;

                float weighted = l.manualAssignment * GetEfficiency(l.sourceTile, l.destTile);
                if (manualIncomingWeightedSumByDestTile.TryGetValue(l.destTile, out float inc))
                    manualIncomingWeightedSumByDestTile[l.destTile] = inc + weighted;
                else
                    manualIncomingWeightedSumByDestTile[l.destTile] = weighted;

                if (l.manualAssignment > 0.01f)
                {
                    if (!manualOutgoingDestsBySourceTile.TryGetValue(l.sourceTile, out var outs))
                    {
                        outs = new List<int>(4);
                        manualOutgoingDestsBySourceTile[l.sourceTile] = outs;
                    }
                    outs.Add(l.destTile);

                    if (!manualIncomingSourcesByDestTile.TryGetValue(l.destTile, out var ins))
                    {
                        ins = new List<int>(4);
                        manualIncomingSourcesByDestTile[l.destTile] = ins;
                    }
                    ins.Add(l.sourceTile);
                }
            }
        }

        private static void ClearDrawIndexLists(Dictionary<int, List<int>> dict)
        {
            foreach (var kv in dict)
                kv.Value.Clear();
            dict.Clear();
        }

        private void RebuildLogisticsCacheEager()
        {
            RebuildPlayerLogisticsRegistryIfNeeded();
            EnsureManualLinkAggregatesFresh();
            logisticsCacheByObjectId ??= new Dictionary<int, LogisticsCacheEntry>();
            logisticsCacheByObjectId.Clear();
            for (int i = 0; i < playerLogisticsNodesOrdered.Count; i++)
            {
                var wo = playerLogisticsNodesOrdered[i].Obj;
                float prod = ComputeDailyProductionUncached(wo);
                float dem = ComputeDailyDemandUncached(wo);
                float inc = GetIncomingManualWeightedSumForTile(wo.Tile);
                float outg = GetOutgoingManualSumForTile(wo.Tile);
                logisticsCacheByObjectId[wo.ID] = new LogisticsCacheEntry
                {
                    DailyProduction = prod,
                    DailyDemand = dem,
                    NetLogistics = prod - dem + inc - outg
                };
            }
            logisticsCacheBuiltGeneration = logisticsCacheGeneration;
        }

        private void EnsureLogisticsCacheFresh()
        {
            if (logisticsCacheBuiltGeneration == logisticsCacheGeneration) return;
            RebuildLogisticsCacheEager();
        }

        public void DrawSelectionLogisticsLines(int tile)
        {
            EnsureManualLinkAggregatesFresh();
            if (manualOutgoingDestsBySourceTile != null && manualOutgoingDestsBySourceTile.TryGetValue(tile, out var outs))
            {
                for (int i = 0; i < outs.Count; i++)
                    DrawLogisticsLine(tile, outs[i]);
            }
            if (manualIncomingSourcesByDestTile != null && manualIncomingSourcesByDestTile.TryGetValue(tile, out var ins))
            {
                for (int i = 0; i < ins.Count; i++)
                    DrawLogisticsLine(ins[i], tile);
            }
        }

        private static void DrawLogisticsLine(int startTile, int endTile)
        {
            Vector3 sPos = Find.WorldGrid.GetTileCenter(startTile);
            Vector3 ePos = Find.WorldGrid.GetTileCenter(endTile);
            Vector3 sLift = sPos + sPos.normalized * 0.05f;
            Vector3 eLift = ePos + ePos.normalized * 0.05f;
            GenDraw.DrawWorldLineBetween(sLift, eLift, LogisticsResources.LogisticsLine, 1.00f);
        }

        public void CompleteSmartLogisticsRefreshNow()
        {
            if (!smartLogisticsDirty) return;

            RebuildPlayerLogisticsRegistryIfNeeded();
            var list = new List<(WorldObject Obj, CompOutpostLogistics Logi)>();
            for (int i = 0; i < playerLogisticsNodesOrdered.Count; i++)
            {
                var pair = playerLogisticsNodesOrdered[i];
                if (LogisticsModeUtil.UsesAutomaticAssignment(pair.Logi.mode))
                    list.Add(pair);
            }
            list.Sort((a, b) => a.Obj.ID.CompareTo(b.Obj.ID));
            BeginManualLinkNotifySuppress();
            try
            {
                LogisticsSmartAssignment.ExecuteAllSmartHubs(this, list);
            }
            finally
            {
                EndManualLinkNotifySuppress();
            }

            smartLogisticsDirty = false;
            smartDirtyFromExplicitNotify = false;
            lastSmartCompletedSoftFingerprint = ComputeSmartSoftFingerprint();
        }

        public void TryReconcileSmartFromFingerprint(string reason)
        {
            if (!AnySmartHub()) return;

            long soft = ComputeSmartSoftFingerprint();
            if (soft == lastSmartCompletedSoftFingerprint)
                return;

            smartLogisticsDirty = true;
            CompleteSmartLogisticsRefreshNow();
        }

        private bool AnySmartHub()
        {
            RebuildPlayerLogisticsRegistryIfNeeded();
            for (int i = 0; i < playerLogisticsNodesOrdered.Count; i++)
            {
                if (LogisticsModeUtil.UsesAutomaticAssignment(playerLogisticsNodesOrdered[i].Logi.mode))
                    return true;
            }
            return false;
        }

        private long ComputeSmartSoftFingerprint()
        {
            RebuildPlayerLogisticsRegistryIfNeeded();
            EnsureLogisticsCacheFresh();
            var s = WorldDominationMod.settings;
            int hubCount = 0;
            int prodCount = 0;
            int pawnPacked = 0;
            int modePacked = 0;
            int surplusPacked = 0;
            for (int i = 0; i < playerLogisticsNodesOrdered.Count; i++)
            {
                var pair = playerLogisticsNodesOrdered[i];
                var wo = pair.Obj;
                hubCount++;
                if (IsFoodProducerOutpost(wo))
                {
                    prodCount++;
                    float surplus = pair.Logi.produceForLogistics
                        ? Mathf.Max(0f, GetDailyProduction(wo) - GetDailyDemand(wo))
                        : 0f;
                    surplusPacked = unchecked(surplusPacked + wo.ID * 31 + Mathf.RoundToInt(surplus * 10f));
                }
                if (wo is Outpost op)
                    pawnPacked = unchecked(pawnPacked + op.AllPawns.Count() * 397 + wo.ID);
                if (LogisticsModeUtil.UsesAutomaticAssignment(pair.Logi.mode))
                    modePacked ^= wo.ID * 17 + (int)LogisticsModeUtil.Normalize(pair.Logi.mode) * 31 + (pair.Logi.produceForLogistics ? 13 : 0);
            }
            int settingsPacked = s == null ? 0 : unchecked(
                s.maxLogisticsRange
                ^ (Mathf.RoundToInt(s.foodConsumptionPerPawn * 100f) << 8)
                ^ (Mathf.RoundToInt(s.foodProductionPerSkill * 100f) << 16));
            unchecked
            {
                return (uint)hubCount ^ ((long)prodCount << 16) ^ pawnPacked ^ modePacked ^ ((long)surplusPacked << 8) ^ settingsPacked;
            }
        }

        private void RunLogisticsPulse()
        {
            TryReconcileSmartFromFingerprint("food accounting pulse");

            RebuildPlayerLogisticsRegistryIfNeeded();
            EnsureLogisticsCacheFresh();
            var nodes = playerLogisticsNodesOrdered;

            pulsePendingChanges.Clear();
            for (int i = 0; i < nodes.Count; i++)
            {
                float prod = GetDailyProduction(nodes[i].Obj);
                float dem = GetDailyDemand(nodes[i].Obj);
                pulsePendingChanges[nodes[i].Logi] = prod - dem;
            }

            // Shipments only from Farming/Hunting hubs with produceForLogistics (WD1 VOE rule).
            for (int hi = 0; hi < nodes.Count; hi++)
            {
                var hub = nodes[hi];
                if (!IsFoodProducerOutpost(hub.Obj) || !hub.Logi.produceForLogistics) continue;
                int hubTile = hub.Obj.Tile;
                for (int li = 0; li < manualLinks.Count; li++)
                {
                    LogisticsLink link = manualLinks[li];
                    if (link.sourceTile != hubTile || link.manualAssignment <= 0.001f) continue;
                    pulsePendingChanges[hub.Logi] -= link.manualAssignment;
                    float eff = GetEfficiency(hubTile, link.destTile);
                    if (eff <= 0f) continue;
                    for (int ci = 0; ci < nodes.Count; ci++)
                    {
                        if (nodes[ci].Obj.Tile != link.destTile) continue;
                        pulsePendingChanges[nodes[ci].Logi] += link.manualAssignment * eff;
                        break;
                    }
                }
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                node.Logi.lastNetChange = pulsePendingChanges[node.Logi];
                node.Logi.currentFood = Mathf.Clamp(node.Logi.currentFood + (node.Logi.lastNetChange * 0.5f), 0, 100f);
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node.Logi.currentFood > 0.001f || GetDailyDemand(node.Obj) <= 0) continue;
                if (!(node.Obj is Outpost op)) continue;
                var occupants = LogisticsResources.OccupantsField?.GetValue(op) as List<Pawn>;
                if (occupants == null || occupants.Count == 0) continue;
                Pawn v = occupants.RandomElement();
                v.Kill(null);
                occupants.Remove(v);
                Find.LetterStack.ReceiveLetter("TSA_StarvationTitle".Translate(), "TSA_StarvationDesc".Translate(v.LabelShort, op.LabelCap), LetterDefOf.Death, op);
                NotifyFoodLogisticsInputsChanged();
            }
        }

        public void ExecuteSmartLogic(WorldObject hub, CompOutpostLogistics logi)
        {
            NotifySmartLogisticsDirty();
            CompleteSmartLogisticsRefreshNow();
        }

        public bool SetAssignment(int src, int dst, float amt, bool markSmartDirty = false)
        {
            const float eps = 0.01f;
            LogisticsLink link = null;
            for (int i = 0; i < manualLinks.Count; i++)
            {
                if (manualLinks[i].sourceTile == src && manualLinks[i].destTile == dst)
                {
                    link = manualLinks[i];
                    break;
                }
            }
            if (link == null)
            {
                if (amt <= eps) return false;
                manualLinks.Add(new LogisticsLink { sourceTile = src, destTile = dst, manualAssignment = amt });
                NotifyManualLinksChanged();
                if (markSmartDirty) NotifySmartLogisticsDirty();
                return true;
            }
            if (Mathf.Abs(link.manualAssignment - amt) <= eps) return false;
            link.manualAssignment = amt;
            NotifyManualLinksChanged();
            if (markSmartDirty) NotifySmartLogisticsDirty();
            return true;
        }
    }
}
