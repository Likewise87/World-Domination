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
using static WorldDomination.SpreadLogEntry;
using Outposts;

namespace WorldDomination
{
    public class SpreadLogEntry : IExposable
    {
        public string message;
        public string labelA;
        public string labelB;
        public GlobalTargetInfo targetA;
        public GlobalTargetInfo targetB;

        public int timestamp;

        public bool isRaid;
        public float attStr, defStr, ratio, winChance;
        public bool victory;
        public float attLossPct, defLossPct;

        public List<string> attDetails = new List<string>();
        public List<string> defDetails = new List<string>();


        public class GlobalWorldStats
        {
            public List<FactionStat> FactionStats = new List<FactionStat>();
            public float[] GlobalTierStr = new float[5];
            public float GlobalTotalStr = 0;
        }

        public class FactionStat
        {
            public Faction faction;
            public int[] counts = new int[5];
            public float[] strength = new float[5];
            public float TotalStr => strength[1] + strength[2] + strength[3] + strength[4];
            public int TotalCount => counts[1] + counts[2] + counts[3] + counts[4];
        }



        public SpreadLogEntry() { }

        // Case: Revolt
        public SpreadLogEntry(string msg, Faction initiator, WorldObject settlement, string oldFactionName, Faction oldFaction)
        {
            this.message = msg;
            this.labelA = $"{initiator.Name} (Rebels)";
            this.targetA = GlobalTargetInfo.Invalid;
            this.labelB = $"{settlement.LabelCap} ({oldFactionName})";
            this.targetB = GlobalTargetInfo.Invalid;
            this.timestamp = Find.TickManager.TicksGame;
        }

        // Case: Standard Actions & Raids
        public SpreadLogEntry(string msg, WorldObject a, WorldObject b = null)
        {
            this.message = msg;
            this.targetA = a;
            this.targetB = b;

            this.labelA = FormatLabel(a);
            this.labelB = FormatLabel(b);

            this.timestamp = Find.TickManager.TicksGame;
        }

        //Case: No Actor
        public SpreadLogEntry(string msg)
        {
            this.message = msg;
            this.timestamp = Find.TickManager.TicksGame;
            this.labelA = "---";
            this.labelB = "---";
        }


        private string FormatLabel(WorldObject obj)
        {
            if (obj == null) return "---";

            var comp = obj.GetComponent<CompViralSpread>();
            string typeLabel = "";

            if (obj is Settlement)
            {
                typeLabel = comp != null ? $" ({comp.tier})" : " (Town)";
            }
            else if (obj.GetType().FullName == "Outposts.Outpost")
            {
                typeLabel = " (Outpost)";
            }

            return $"{obj.LabelCap}{typeLabel} ({obj.Faction?.Name ?? "No Faction"})";
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref message, "message");
            Scribe_Values.Look(ref labelA, "labelA");
            Scribe_Values.Look(ref labelB, "labelB");
            Scribe_Values.Look(ref timestamp, "timestamp", 0);
            Scribe_TargetInfo.Look(ref targetA, "targetA");
            Scribe_TargetInfo.Look(ref targetB, "targetB");
            Scribe_Values.Look(ref isRaid, "isRaid", false);
            Scribe_Values.Look(ref attStr, "attStr", 0f);
            Scribe_Values.Look(ref defStr, "defStr", 0f);
            Scribe_Values.Look(ref ratio, "ratio", 0f);
            Scribe_Values.Look(ref winChance, "winChance", 0f);
            Scribe_Values.Look(ref victory, "victory", false);
            Scribe_Values.Look(ref attLossPct, "attLossPct", 0f);
            Scribe_Values.Look(ref defLossPct, "defLossPct", 0f);
            Scribe_Collections.Look(ref attDetails, "attDetails", LookMode.Value);
            Scribe_Collections.Look(ref defDetails, "defDetails", LookMode.Value);
        }
    }

    public static class SpaceMapGuard
    {
        /// <summary>Core game uses RimWorld.OrbitLayer for space; avoid hard type reference.</summary>
        public static bool IsOrbitLayer(PlanetLayer layer)
        {
            return layer != null && layer.GetType().Name == "OrbitLayer";
        }

        public static bool IsSpaceLike(Map map)
        {
            if (map == null) return false;
            string biome = map.Biome?.defName ?? string.Empty;
            if (biome.IndexOf("space", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (biome.IndexOf("asteroid", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (biome.IndexOf("vacuum", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            string parent = map.Parent?.def?.defName ?? string.Empty;
            if (parent.IndexOf("Space", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (parent.IndexOf("Asteroid", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (parent.IndexOf("Odyssey", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (parent.IndexOf("Ship", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (parent.IndexOf("Grav", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            if (map.Tile < 0) return true;
            return false;
        }

        public static bool IsSpaceLike(WorldObject obj)
        {
            if (obj == null) return false;

            // Handle specific Settlement logic if it has a map
            if (obj is Settlement s && s.Map != null) return IsSpaceLike(s.Map);

            string defName = obj.def?.defName ?? string.Empty;
            bool isSpaceDef = defName.IndexOf("Space", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              defName.IndexOf("Asteroid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                              defName.IndexOf("Odyssey", StringComparison.OrdinalIgnoreCase) >= 0;

            return isSpaceDef || obj.Tile < 0;
        }
    }

    public static class WorldActions_Utils
    {
        /// <summary>True only when tile layer is readable and is orbit — safe for stripping comps; false if world/tile not ready.</summary>
        public static bool IsConfirmedOrbitTile(int tile)
        {
            WorldGrid grid = Find.WorldGrid;
            if (grid == null || Find.World == null) return false;
            if (tile < 0 || tile >= grid.TilesCount) return false;
            try
            {
                PlanetLayer layer = Find.World.grid[tile].Layer;
                if (layer == null) return false;
                return SpaceMapGuard.IsOrbitLayer(layer);
            }
            catch (Exception ex)
            {
                Log.Warning($"[WD] IsConfirmedOrbitTile: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        public static bool IsConfirmedOrbitWorldObject(WorldObject o) =>
            o != null && IsConfirmedOrbitTile(o.Tile);

        /// <summary>WD simulation scope: not confirmed orbit. Unknown tiles stay in-scope so init does not strip everything.</summary>
        public static bool IsWdSurfaceTile(int tile) => !IsConfirmedOrbitTile(tile);

        public static bool IsWdSurfaceWorldObject(WorldObject o) =>
            o != null && IsWdSurfaceTile(o.Tile);

        public static void EnsureAllSettlementsInitialized()
        {
            var settlements = Find.WorldObjects.Settlements;
            for (int i = 0; i < settlements.Count; i++)
            {
                Settlement s = settlements[i];
                if (s.Faction == null || s.Faction.def.hidden || s.Faction.defeated || IsExcludedFaction(s.Faction)) continue;
                if (!IsWdSurfaceWorldObject(s)) continue;
                var comp = s.GetComponent<CompViralSpread>();
                if (comp == null || !string.IsNullOrEmpty(comp.subType) || comp.IsOutpost) continue;
                ApplyRandomTier(comp);
            }
        }

        public static bool IsInPlayerInfluence(int tileID)
        {
            // FIX: Guard against null player faction during world gen/init
            Faction playerFact = Faction.OfPlayer;
            if (playerFact == null) return false;

            var playerColony = Find.WorldObjects.Settlements.FirstOrDefault(s => s.Faction == playerFact);
            if (playerColony == null) return false;

            float radius = GetPlayerInfluenceRadius(playerColony.Tile);
            return Find.WorldGrid.ApproxDistanceInTiles(tileID, playerColony.Tile) <= radius;
        }

        public static GlobalWorldStats GetWorldPowerStats()
        {
            GlobalWorldStats stats = new GlobalWorldStats();

            // 1. Process AI Settlements
            // Group settlements by faction to avoid repeated O(n) lookups in the loop
            var settlementsByFaction = Find.WorldObjects.Settlements
                .Where(s => s.Faction != null && !s.Faction.IsPlayer)
                .GroupBy(s => s.Faction)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Loop through all visible NPC factions
            foreach (var f in Find.FactionManager.AllFactionsVisible.Where(x => !x.IsPlayer && !x.def.hidden))
            {
                var fs = new FactionStat { faction = f };

                if (settlementsByFaction.TryGetValue(f, out var fSettlements))
                {
                    foreach (var s in fSettlements)
                    {
                        if (!IsWdSurfaceWorldObject(s)) continue;
                        var comp = s.GetComponent<CompViralSpread>();
                        if (comp == null) continue;

                        // Clamp tier to 1-4 range (internally 0-3 + 1)
                        int t = Mathf.Clamp((int)comp.tier + 1, 1, 4);
                        fs.counts[t]++;
                        fs.strength[t] += comp.strength;

                        // Update global accumulators for share calculations
                        stats.GlobalTierStr[t] += comp.strength;
                        stats.GlobalTotalStr += comp.strength;
                    }
                }

                // Only add factions that actually have a world presence
                if (fs.TotalCount > 0)
                    stats.FactionStats.Add(fs);
            }

            // 2. Process Player Outposts
            Faction pFaction = Find.FactionManager.OfPlayer;
            if (pFaction != null)
            {
                var playerStats = GetPlayerOutpostStats(stats);
                if (playerStats != null && playerStats.TotalCount > 0)
                {
                    stats.FactionStats.Add(playerStats);
                }
            }

            // Sort by influence so the UI and Coalitions know who is on top
            stats.FactionStats = stats.FactionStats.OrderByDescending(x => x.TotalStr).ToList();
            return stats;
        }

        private static FactionStat GetPlayerOutpostStats(GlobalWorldStats globalStats)
        {
            // FIX: Use local variable and check for null immediately
            Faction pFaction = Faction.OfPlayer;
            if (pFaction == null) return new FactionStat();

            var fs = new FactionStat { faction = pFaction };
            var occupantsField = typeof(Outpost).GetField("occupants", BindingFlags.Instance | BindingFlags.NonPublic);

            var playerOutposts = Find.WorldObjects.AllWorldObjects.OfType<Outpost>()
                .Where(o => o.Faction == pFaction);

            foreach (var outpost in playerOutposts)
            {
                if (!IsWdSurfaceWorldObject(outpost)) continue;
                var comp = outpost.GetComponent<CompViralSpread>();
                int pawnCount = 0;
                if (occupantsField?.GetValue(outpost) is System.Collections.IList occupants) pawnCount = occupants.Count;

                float str = comp?.strength ?? 0f;

                int t = 1;
                if (pawnCount > 10) t = 4;
                else if (pawnCount > 7) t = 3;
                else if (pawnCount > 3) t = 2;

                fs.counts[t]++;
                fs.strength[t] += str;

                globalStats.GlobalTierStr[t] += str;
                globalStats.GlobalTotalStr += str;
            }
            return fs;
        }


        public static void ApplyRandomTier(CompViralSpread comp)
        {
            var s = WorldDominationMod.settings;

            float totalWeight = s.genWeightT1 + s.genWeightT2 + s.genWeightT3 + s.genWeightT4;
            if (totalWeight <= 0) { comp.SetState(SettlementTier.T1); return; }

            float rand = Rand.Range(0f, totalWeight);

            if (rand < s.genWeightT1)
                comp.SetState(SettlementTier.T1);
            else if (rand < s.genWeightT1 + s.genWeightT2)
                comp.SetState(SettlementTier.T2);
            else if (rand < s.genWeightT1 + s.genWeightT2 + s.genWeightT3)
                comp.SetState(SettlementTier.T3);
            else
                comp.SetState(SettlementTier.T4);
        }

        public static bool IsExcludedFaction(Faction f)
        {
            if (f == null || f.def == null || f.IsPlayer) return true;
            if (f == Faction.OfTradersGuild) return true;

            string defName = f.def.defName ?? string.Empty;
            if (defName.IndexOf("Insect", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (defName.IndexOf("Hive", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            string label = f.Name?.ToString() ?? string.Empty;
            if (label.IndexOf("insect", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (label.IndexOf("hive", StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        public static bool IsSettlementProtected(Settlement s)
        {
            if (s == null) return true;
            if (!IsWdSurfaceWorldObject(s)) return true;

            if (s.HasMap) return true;
            if (Find.WorldObjects.AnyWorldObjectOfDefAt(WorldObjectDefOf.Caravan, s.Tile)) return true;

            // OPTIMIZED: Use Settlements sub-list instead of AllWorldObjects
            if (Find.WorldObjects.SettlementAt(s.Tile) is Settlement other && other.Faction != null && other.Faction.IsPlayer) return true;

            if (Find.QuestManager.QuestsListForReading.Any(q => q.State == QuestState.Ongoing && q.QuestLookTargets.Contains(s)))
                return true;

            if (s.Faction == null || IsExcludedFaction(s.Faction) || s.Faction.defeated)
                return true;

            return false;
        }


        public static float GetPlayerInfluenceRadius(int tile)
        {
            var seth = WorldDominationMod.settings;
            float radius = seth.playerInfluenceBase + (GenDate.DaysPassed * seth.playerInfluenceTimeRate);
            Map map = Current.Game.Maps.FirstOrDefault(m => m.Tile == tile);
            if (map != null) radius += (map.wealthWatcher.WealthTotal / 5000f * seth.playerInfluenceWealthRate);
            return radius;
        }

        public static void RefreshMap()
        {
            try
            {
                var layersField = typeof(WorldRenderer).GetField("layers", BindingFlags.Instance | BindingFlags.NonPublic);
                if (layersField != null)
                {
                    var list = layersField.GetValue(Find.World.renderer) as System.Collections.IEnumerable;
                    if (list != null)
                    {
                        foreach (object layer in list) layer.GetType().GetMethod("SetDirty")?.Invoke(layer, null);
                    }
                }
            }
            catch { }
        }

        /// <summary>Legacy name: off WD surface (confirmed orbit or invalid tile index). Prefer <see cref="IsWdSurfaceTile"/> for new code.</summary>
        public static bool IsSpace(WorldObject obj)
        {
            if (obj == null) return false;
            return !IsWdSurfaceWorldObject(obj);
        }

        public static bool IsVassal(WorldObject obj)
        {
            if (obj == null) return false;
            // We check the class name as a string to avoid a hard dependency crash
            return obj.GetType().FullName == "FactionTerritories.Vassalise.FactionTerritories_VassalOutpost";
        }

        public static bool IsSpace(int tile, WorldComponent_SpreadManager manager = null)
        {
            if (tile < 0 || tile >= Find.WorldGrid.TilesCount) return true;
            return !IsWdSurfaceTile(tile);
        }

        public static int GetDistance(int tileA, int tileB, WorldComponent_SpreadManager manager)
        {
            if (tileA == tileB) return 0;

            int t1 = Math.Min(tileA, tileB);
            int t2 = Math.Max(tileA, tileB);
            long key = ((long)t1 << 32) | (uint)t2;

            if (!manager.distanceCache.TryGetValue(key, out int dist))
            {
                const int DistanceCacheMaxEntries = 65536;
                if (manager.distanceCache.Count >= DistanceCacheMaxEntries)
                    manager.distanceCache.Clear();

                dist = Mathf.RoundToInt(Find.WorldGrid.ApproxDistanceInTiles(t1, t2));
                manager.distanceCache[key] = dist;
            }
            return dist;
        }

        public static void RerollAllSettlements()
        {
            // 1. Get all NPC settlements that have our component
            var settlements = Find.WorldObjects.Settlements
                .Where(s => s.Faction != null && !s.Faction.IsPlayer)
                .ToList();

            foreach (var s in settlements)
            {
                if (!IsWdSurfaceWorldObject(s)) continue;
                var comp = s.GetComponent<CompViralSpread>();
                if (comp == null) continue;

                // 2. Clear current state to avoid logic conflicts during SetState
                comp.strength = 0;

                // 3. Apply the new weights from your settings
                ApplyRandomTier(comp);

                // 4. Force a name recache if you have tier-specific naming
                s.Name = s.Name;
            }

            // 5. Vital: Tell the managers to recount the world power
            Find.World.GetComponent<WorldComponent_SpreadManager>()?.Notify_WeightsChanged();

            Messages.Message("TSA_WD_Message_RerollSuccess".Translate(settlements.Count), MessageTypeDefOf.TaskCompletion);
        }

        public static bool HasActiveQuest(Settlement settlement)
        {
            if (settlement == null) return false;
            var quests = Find.QuestManager.QuestsListForReading;
            for (int i = 0; i < quests.Count; i++)
            {
                if (quests[i].State != QuestState.Ongoing) continue;
                foreach (var t in quests[i].QuestLookTargets)
                {
                    if (t.WorldObject == settlement) return true;
                }
            }
            return false;
        }
    }
}