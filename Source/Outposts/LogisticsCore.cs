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
    public enum LogisticsMode { Manual, Equal, FullyFeed, BiggestDeficit }

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

        // Toggle to determine if this outpost feeds the network or the colony
        public bool produceForLogistics = false;

        public override void Initialize(WorldObjectCompProperties props)
        {
            base.Initialize(props);
            if (parent.def.defName.Contains("Farming") || parent.def.defName.Contains("Hunting"))
            {
                this.mode = LogisticsMode.FullyFeed;
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
        }

        public override string CompInspectStringExtra()
        {
            if (!WorldDominationMod.settings.foodLogisticsActive) return null;

            string balanceStr = lastNetChange >= 0 ? $"+{lastNetChange:F1}" : $"{lastNetChange:F1}";
            Color col = lastNetChange > 0.1f ? Color.green : (lastNetChange < -0.1f ? Color.red : Color.yellow);
            string modeStr = mode == LogisticsMode.Manual ? "" : $" [{("TSA_LogisticsMode_" + mode).Translate()}]";

            string baseStr = "TSA_FoodSupplyInspect".Translate(
                currentFood.ToString("F1"),
                MaxFood.ToString("F0"),
                balanceStr.Colorize(col)
            ) + modeStr;

            if (produceForLogistics)
                baseStr += "\n" + "TSA_LogisticsActive".Translate().Colorize(Color.cyan);

            return baseStr;
        }

        public override void PostDrawExtraSelectionOverlays()
        {
            base.PostDrawExtraSelectionOverlays();

            if (parent is Outpost outpost)
            {
                var manager = Find.World.GetComponent<WorldComponent_LogisticsManager>();
                if (manager == null) return;

                bool isSelected = Find.WorldSelector.IsSelected(outpost);
                bool isProducer = outpost.def.defName.Contains("Farming") || outpost.def.defName.Contains("Hunting");

                if (isSelected)
                {
                    if (isProducer)
                    {
                        var s = WorldDominationMod.settings;
                        GenDraw.DrawWorldRadiusRing(outpost.Tile, s.maxLogisticsRange);
                    }

                    // Draw outgoing lines (if producer)
                    var outgoingLinks = manager.manualLinks.Where(l => l.sourceTile == outpost.Tile && l.manualAssignment > 0.01f);
                    foreach (var link in outgoingLinks)
                    {
                        DrawLogisticsLine(outpost.Tile, link.destTile);
                    }

                    // Draw incoming lines (if consumer)
                    var incomingLinks = manager.manualLinks.Where(l => l.destTile == outpost.Tile && l.manualAssignment > 0.01f);
                    foreach (var link in incomingLinks)
                    {
                        DrawLogisticsLine(link.sourceTile, outpost.Tile);
                    }
                }
            }
        }

        private void DrawLogisticsLine(int startTile, int endTile)
        {
            // Simple logic: Use direct tile centers with minimal vertical offset (0.05f)
            Vector3 sPos = Find.WorldGrid.GetTileCenter(startTile);
            Vector3 ePos = Find.WorldGrid.GetTileCenter(endTile);

            Vector3 sLift = sPos + sPos.normalized * 0.05f;
            Vector3 eLift = ePos + ePos.normalized * 0.05f;

            GenDraw.DrawWorldLineBetween(sLift, eLift, MaterialPool.MatFrom("UI/Overlays/ThingLine", ShaderDatabase.WorldOverlayTransparent, Color.white, 3590), 1.00f);
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

                // Check if it's edible and has nutrition (handles meals, raw food, and modded items)
                if (t.def.ingestible == null) continue;
                float nutritionValue = t.def.GetStatValueAbstract(StatDefOf.Nutrition);
                if (nutritionValue <= 0) continue;

                // Calculate how many items we can fit based on their individual nutrition
                // Example: 1 Simple Meal (0.9 nut) takes 0.9 units of space.
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
            {
                t.Destroy();
            }

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
            // 1. Pass through existing gizmos
            if (__result != null)
            {
                foreach (var g in __result) yield return g;
            }

            // 2. Gate: Faction and Global Setting
            if (__instance.Faction != Faction.OfPlayer) yield break;
            if (!WorldDominationMod.settings.foodLogisticsActive) yield break;

            var logiComp = __instance.GetComponent<CompOutpostLogistics>();
            if (logiComp == null) yield break;

            // 3. Logic: Toggle for Logistics Producers (Farming/Hunting)
            if (__instance.def.defName.Contains("Farming") || __instance.def.defName.Contains("Hunting"))
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "TSA_ProduceForLogistics".Translate(),
                    defaultDesc = "TSA_ProduceForLogisticsDesc".Translate(),
                    icon = logiComp.produceForLogistics
                        ? ContentFinder<Texture2D>.Get("UI/Commands/LogisticsMode")
                        : ContentFinder<Texture2D>.Get("UI/Commands/LogisticsMode_Off"),
                    isActive = () => logiComp.produceForLogistics,
                    toggleAction = () => {
                        logiComp.produceForLogistics = !logiComp.produceForLogistics;
                        if (logiComp.mode != LogisticsMode.Manual)
                        {
                            var manager = Find.World.GetComponent<WorldComponent_LogisticsManager>();
                            manager?.ExecuteSmartLogic(__instance, logiComp);
                        }
                    }
                };
            }

            // 4. Logic: Convert Food Gizmo (Visible on ALL outposts if capacity allows)
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
            // SURGICAL FIX: Only hijack the string if logistics are enabled globally
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
            // SURGICAL FIX: If logistics are disabled globally, don't freeze production
            if (!WorldDominationMod.settings.foodLogisticsActive) return;

            var logi = __instance.GetComponent<CompOutpostLogistics>();
            if (logi != null && logi.produceForLogistics)
            {
                var ticksField = typeof(Outpost).GetField("ticksTillProduction", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (ticksField != null)
                {
                    int currentTimer = (int)ticksField.GetValue(__instance);
                    ticksField.SetValue(__instance, currentTimer + delta);
                }
            }
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

        public List<LogisticsLink> manualLinks = new List<LogisticsLink>();
        private int nextTick = -1;
        private const int CheckInterval = 6000;

        public WorldComponent_LogisticsManager(World world) : base(world) { }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref manualLinks, "manualLinks", LookMode.Deep);
            if (manualLinks == null) manualLinks = new List<LogisticsLink>();
        }

        public override void WorldComponentTick()
        {
            var s = WorldDominationMod.settings;
            if (!s.foodLogisticsActive) return;

            if (Find.TickManager.TicksGame >= nextTick || nextTick == -1)
            {
                RunLogisticsPulse();
                nextTick = Find.TickManager.TicksGame + CheckInterval;
            }
        }

        public float GetEfficiency(int start, int end)
        {
            var s = WorldDominationMod.settings;
            float dist = Find.WorldGrid.ApproxDistanceInTiles(start, end);
            float max = (float)s.maxLogisticsRange;

            if (dist <= max * 0.6f) return s.effAt60;
            if (dist <= max * 0.8f) return s.effAt80;
            if (dist <= max) return s.effAt100;

            return 0f;
        }

        public float GetDailyProduction(WorldObject obj)
        {
            var s = WorldDominationMod.settings;
            if (!(obj is Outpost op)) return s.foodConsumptionPerPawn * 2f;

            float production = s.foodConsumptionPerPawn * 2f;

            bool isFoodProducer = op.def.defName.Contains("Farming") || op.def.defName.Contains("Hunting");
            if (isFoodProducer)
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

        public float GetDailyDemand(WorldObject obj)
        {
            var s = WorldDominationMod.settings;
            float count = 0f;
            if (obj is MapParent mapParent && mapParent.HasMap) count = (float)mapParent.Map.mapPawns.AllPawnsCount;
            else if (obj is Outpost outpost) count = (float)outpost.AllPawns.Count();

            return count * s.foodConsumptionPerPawn;
        }

        private void RunLogisticsPulse()
        {
            var nodes = Find.WorldObjects.AllWorldObjects
                .Where(x => x.Faction == Faction.OfPlayer)
                .Select(x => new { Obj = x, Logi = x.GetComponent<CompOutpostLogistics>() })
                .Where(x => x.Logi != null).ToList();

            foreach (var node in nodes.Where(n => n.Logi.mode != LogisticsMode.Manual))
            {
                ExecuteSmartLogic(node.Obj, node.Logi);
            }

            Dictionary<CompOutpostLogistics, float> pendingChanges = new Dictionary<CompOutpostLogistics, float>();
            foreach (var node in nodes) pendingChanges[node.Logi] = GetDailyProduction(node.Obj) - GetDailyDemand(node.Obj);

            foreach (var hub in nodes.Where(x => (x.Obj.def.defName.Contains("Farming") || x.Obj.def.defName.Contains("Hunting")) && x.Logi.produceForLogistics))
            {
                foreach (var link in manualLinks.Where(l => l.sourceTile == hub.Obj.Tile))
                {
                    var consumer = nodes.FirstOrDefault(c => c.Obj.Tile == link.destTile);
                    if (consumer == null) continue;
                    float eff = GetEfficiency(hub.Obj.Tile, consumer.Obj.Tile);

                    pendingChanges[hub.Logi] -= link.manualAssignment;
                    pendingChanges[consumer.Logi] += (link.manualAssignment * eff);
                }
            }

            foreach (var node in nodes)
            {
                node.Logi.lastNetChange = pendingChanges[node.Logi];
                node.Logi.currentFood = Mathf.Clamp(node.Logi.currentFood + (node.Logi.lastNetChange * 0.5f), 0, 100f);
            }

            foreach (var node in nodes)
            {
                if (node.Logi.currentFood <= 0.001f && GetDailyDemand(node.Obj) > 0 && node.Obj is Outpost op)
                {
                    var occupants = typeof(Outpost).GetField("occupants", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(op) as List<Pawn>;
                    if (occupants?.Count > 0)
                    {
                        Pawn v = occupants.RandomElement();
                        v.Kill(null);
                        occupants.Remove(v);
                        Find.LetterStack.ReceiveLetter("TSA_StarvationTitle".Translate(), "TSA_StarvationDesc".Translate(v.LabelShort, op.LabelCap), LetterDefOf.Death, op);
                    }
                }
            }
        }

        public void ExecuteSmartLogic(WorldObject hub, CompOutpostLogistics logi)
        {
            var s = WorldDominationMod.settings;
            float budget = logi.produceForLogistics ? Mathf.Max(0, GetDailyProduction(hub) - GetDailyDemand(hub)) : 0f;

            var links = manualLinks.Where(l => l.sourceTile == hub.Tile).ToList();
            foreach (var l in links) l.manualAssignment = 0;

            var allNearby = Find.WorldObjects.AllWorldObjects
                .Where(o => o.Faction == Faction.OfPlayer && o != hub && o.GetComponent<CompOutpostLogistics>() != null
                       && Find.WorldGrid.ApproxDistanceInTiles(hub.Tile, o.Tile) <= s.maxLogisticsRange)
                .Select(o => new { Obj = o, Logi = o.GetComponent<CompOutpostLogistics>(), Def = GetDailyProduction(o) - GetDailyDemand(o) })
                .ToList();

            if (!allNearby.Any() || budget <= 0) return;

            var deficitTargets = allNearby.Where(x => x.Def < -0.1f).ToList();
            if (deficitTargets.Any())
            {
                switch (logi.mode)
                {
                    case LogisticsMode.Equal:
                        float perOutpost = budget / deficitTargets.Count;
                        foreach (var t in deficitTargets) SetAssignment(hub.Tile, t.Obj.Tile, perOutpost);
                        budget = 0;
                        break;

                    case LogisticsMode.FullyFeed:
                        foreach (var t in deficitTargets.OrderByDescending(x => x.Def))
                        {
                            float eff = GetEfficiency(hub.Tile, t.Obj.Tile);
                            if (eff <= 0) continue;
                            float needed = (Mathf.Abs(t.Def) + 0.1f) / eff;
                            if (budget >= needed) { SetAssignment(hub.Tile, t.Obj.Tile, needed); budget -= needed; }
                        }
                        foreach (var t in deficitTargets.OrderBy(x => x.Def))
                        {
                            if (budget <= 0.05f) break;
                            var currentLink = manualLinks.FirstOrDefault(l => l.sourceTile == hub.Tile && l.destTile == t.Obj.Tile);
                            float assigned = currentLink?.manualAssignment ?? 0f;
                            float eff = GetEfficiency(hub.Tile, t.Obj.Tile);
                            if (eff <= 0) continue;
                            float cap = (Mathf.Abs(t.Def) + 0.1f) / eff;
                            float canTake = Mathf.Max(0, cap - assigned);
                            float toGive = Mathf.Min(budget, canTake);
                            SetAssignment(hub.Tile, t.Obj.Tile, assigned + toGive);
                            budget -= toGive;
                        }
                        break;

                    case LogisticsMode.BiggestDeficit:
                        foreach (var t in deficitTargets.OrderBy(x => x.Def))
                        {
                            if (budget <= 0.05f) break;
                            float eff = GetEfficiency(hub.Tile, t.Obj.Tile);
                            if (eff <= 0) continue;
                            float needed = (Mathf.Abs(t.Def) + 0.1f) / eff;
                            float toGive = Mathf.Min(needed, budget);
                            SetAssignment(hub.Tile, t.Obj.Tile, toGive);
                            budget -= toGive;
                        }
                        break;
                }
            }

            if (budget > 0.1f)
            {
                foreach (var t in allNearby.OrderBy(x => x.Logi.currentFood))
                {
                    if (budget <= 0.05f) break;
                    float eff = GetEfficiency(hub.Tile, t.Obj.Tile);
                    if (eff <= 0) continue;

                    float currentAssignment = manualLinks.FirstOrDefault(l => l.sourceTile == hub.Tile && l.destTile == t.Obj.Tile)?.manualAssignment ?? 0f;
                    float toSend = budget;
                    SetAssignment(hub.Tile, t.Obj.Tile, currentAssignment + toSend);
                    budget -= toSend;
                }
            }
        }

        private void SetAssignment(int src, int dst, float amt)
        {
            var link = manualLinks.FirstOrDefault(l => l.sourceTile == src && l.destTile == dst);
            if (link == null) manualLinks.Add(new LogisticsLink { sourceTile = src, destTile = dst, manualAssignment = amt });
            else link.manualAssignment = amt;
        }
    }
}