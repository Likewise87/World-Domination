using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using WorldDomination;
using TSA_WorldDomination.Settings;
using Outposts;

namespace TSA_WorldDomination.WorldActions
{
    public static class WorldActions_Roads
    {
        public static void AttemptBuildRoad(Settlement actor, CompViralSpread comp, WorldComponent_SpreadManager manager)
        {
            var seth = WorldDominationMod.settings;

            var closestEnemy = Find.WorldObjects.Settlements
                .Where(s => s.Faction != null && s.Faction.HostileTo(actor.Faction) && !s.Faction.def.hidden)
                .OrderBy(s => WorldActions_Utils.GetDistance(actor.Tile, s.Tile, manager))
                .FirstOrDefault();

            if (closestEnemy != null)
            {
                int enemyDist = WorldActions_Utils.GetDistance(actor.Tile, closestEnemy.Tile, manager);
                if (enemyDist <= 5)
                {
                    manager.AddLog(new SpreadLogEntry("TSA_WD_Log_Road_AbortedDanger".Translate(actor.LabelCap, closestEnemy.LabelCap, enemyDist), actor, closestEnemy)
                    {
                        labelA = actor.LabelCap,
                        labelB = closestEnemy.LabelCap
                    });
                    return;
                }
            }

            var target = Find.WorldObjects.Settlements
                .Where(s => s != actor && s.Faction != null && !s.Faction.def.hidden)
                .Where(s =>
                {
                    if (s.Faction == actor.Faction) return true;
                    return actor.Faction.RelationKindWith(s.Faction) == FactionRelationKind.Ally;
                })
                .OrderByDescending(s => s.Faction == actor.Faction)
                .ThenBy(s => WorldActions_Utils.GetDistance(actor.Tile, s.Tile, manager))
                .FirstOrDefault();

            if (target == null) return;

            int dist = WorldActions_Utils.GetDistance(actor.Tile, target.Tile, manager);
            if (dist > seth.maxRoadRange) return;

            TryBuildRoadStep(actor, target, manager);
        }

        public static void TryBuildRoadStepToTile(Outpost actor, int targetTileId)
        {
            var manager = Find.World.GetComponent<WorldComponent_SpreadManager>();
            var planetLayerKV = Find.WorldGrid.PlanetLayers.FirstOrDefault();
            PlanetLayer layer = planetLayerKV.Value;
            if (layer == null) return;

            PlanetTile startTile = new PlanetTile(actor.Tile, layer);
            PlanetTile destTile = new PlanetTile(targetTileId, layer);

            using (WorldPathing pathing = new WorldPathing(layer))
            {
                using (WorldPath path = pathing.FindPath(startTile, destTile, null))
                {
                    if (path == null || !path.Found)
                    {
                        actor.GetComponent<CompViralSpread>().roadTargetTile = -1;
                        return;
                    }

                    var nodes = path.NodesReversed;
                    if (nodes.Count < 2) return;

                    RoadDef actorPlannedRoad = GetRoadDefForActor(actor);
                    int firstGapIndex = -1;
                    int actualUnpavedCount = 0;

                    for (int i = nodes.Count - 1; i > 0; i--)
                    {
                        if (ShouldUpgradeRoad(nodes[i], nodes[i - 1], actorPlannedRoad))
                        {
                            if (firstGapIndex == -1) firstGapIndex = i;
                            actualUnpavedCount++;
                        }
                    }

                    if (firstGapIndex != -1)
                    {
                        ApplyRoadLink(nodes[firstGapIndex], nodes[firstGapIndex - 1], actor);
                        int remaining = actualUnpavedCount - 1;

                        manager.AddLog(new SpreadLogEntry("TSA_WD_Log_OutpostRoadProgress".Translate(actor.LabelCap, remaining), actor, null));

                        if (remaining <= 0)
                        {
                            actor.GetComponent<CompViralSpread>().roadTargetTile = -1;
                            Messages.Message("TSA_WD_RoadFinished".Translate(actor.LabelCap), MessageTypeDefOf.PositiveEvent);
                        }
                    }
                    else
                    {
                        actor.GetComponent<CompViralSpread>().roadTargetTile = -1;
                    }
                }
            }
        }

        private static void TryBuildRoadStep(Settlement actor, Settlement target, WorldComponent_SpreadManager manager)
        {
            var planetLayerKV = Find.WorldGrid.PlanetLayers.FirstOrDefault();
            PlanetLayer layer = planetLayerKV.Value;
            if (layer == null) return;

            PlanetTile startTile = new PlanetTile(actor.Tile, layer);
            PlanetTile destTile = new PlanetTile(target.Tile, layer);

            using (WorldPathing pathing = new WorldPathing(layer))
            {
                using (WorldPath path = pathing.FindPath(startTile, destTile, null))
                {
                    if (path == null || !path.Found) return;
                    var nodes = path.NodesReversed;
                    if (nodes.Count < 2) return;

                    RoadDef plannedRoad = GetRoadDefForActor(actor);
                    int firstGapIndex = -1;
                    int actualUnpavedCount = 0;

                    for (int i = nodes.Count - 1; i > 0; i--)
                    {
                        if (ShouldUpgradeRoad(nodes[i], nodes[i - 1], plannedRoad))
                        {
                            if (firstGapIndex == -1) firstGapIndex = i;
                            actualUnpavedCount++;
                        }
                    }

                    if (firstGapIndex != -1)
                    {
                        ApplyRoadLink(nodes[firstGapIndex], nodes[firstGapIndex - 1], actor);
                        int remainingAfterThis = actualUnpavedCount - 1;

                        string message = (remainingAfterThis == 0)
                            ? "TSA_WD_Log_Road_Finished".Translate(actor.LabelCap, target.LabelCap)
                            : "TSA_WD_Log_Road_Progress".Translate(actor.LabelCap, target.LabelCap, remainingAfterThis);

                        manager.AddLog(new SpreadLogEntry(message, actor, target));
                    }
                }
            }
        }

        public static int GetCurrentWorkTile(int startTileId, int destTileId, SettlementTier requestedTier = SettlementTier.T1)
        {
            var planetLayerKV = Find.WorldGrid.PlanetLayers.FirstOrDefault();
            PlanetLayer layer = planetLayerKV.Value;
            if (layer == null) return -1;

            RoadDef targetRoad = GetRoadDefByTier(requestedTier);

            using (WorldPathing pathing = new WorldPathing(layer))
            {
                using (WorldPath path = pathing.FindPath(new PlanetTile(startTileId, layer), new PlanetTile(destTileId, layer), null))
                {
                    if (path == null || !path.Found) return -1;
                    var nodes = path.NodesReversed;
                    for (int i = nodes.Count - 1; i > 0; i--)
                    {
                        if (ShouldUpgradeRoad(nodes[i], nodes[i - 1], targetRoad)) return nodes[i - 1].tileId;
                    }
                }
            }
            return -1;
        }

        private static bool ShouldUpgradeRoad(PlanetTile a, PlanetTile b, RoadDef plannedRoad)
        {
            if (plannedRoad == null) return false;
            var tile = Find.WorldGrid[a.tileId];

            var field = tile.GetType().GetField("potentialRoads");
            if (field == null) return true; // Safe fallback: assume work is needed if field is missing

            var roadsList = field.GetValue(tile) as System.Collections.IList;
            if (roadsList == null || roadsList.Count == 0) return true;

            foreach (var link in roadsList)
            {
                var neighborField = link.GetType().GetField("neighbor");
                var roadField = link.GetType().GetField("road");

                if (neighborField != null && roadField != null)
                {
                    var neighborTile = (PlanetTile)neighborField.GetValue(link);
                    if (neighborTile.tileId == b.tileId)
                    {
                        var existingRoad = (RoadDef)roadField.GetValue(link);
                        return existingRoad == null || plannedRoad.priority > existingRoad.priority;
                    }
                }
            }
            return true;
        }

        private static void ApplyRoadLink(PlanetTile a, PlanetTile b, WorldObject actor)
        {
            RoadDef road = GetRoadDefForActor(actor);
            if (road == null) return;

            AddLink(a.tileId, b, road);
            AddLink(b.tileId, a, road);

            foreach (var drawLayer in Find.World.renderer.AllDrawLayers)
                if (drawLayer.GetType().Name.Contains("Road")) drawLayer.SetDirty();
        }

        private static void AddLink(int tileId, PlanetTile neighbor, RoadDef road)
        {
            var tile = Find.WorldGrid[tileId];
            var field = tile.GetType().GetField("potentialRoads");
            if (field == null) return;

            var roadsList = field.GetValue(tile) as System.Collections.IList;
            if (roadsList == null)
            {
                roadsList = Activator.CreateInstance(field.FieldType) as System.Collections.IList;
                field.SetValue(tile, roadsList);
            }

            for (int i = 0; i < roadsList.Count; i++)
            {
                var link = roadsList[i];
                var nField = link.GetType().GetField("neighbor");
                if (nField != null && ((PlanetTile)nField.GetValue(link)).tileId == neighbor.tileId)
                {
                    roadsList.RemoveAt(i);
                    break;
                }
            }

            var linkType = field.FieldType.GetGenericArguments()[0];
            var newLink = Activator.CreateInstance(linkType);
            linkType.GetField("neighbor").SetValue(newLink, neighbor);
            linkType.GetField("road").SetValue(newLink, road);
            roadsList.Add(newLink);
        }

        private static RoadDef GetRoadDefByTier(SettlementTier tier, TechLevel tech = TechLevel.Industrial)
        {
            // Tier 3: Asphalt for Industrial+, Stone for everyone else
            if (tier == SettlementTier.T3)
            {
                if (tech >= TechLevel.Industrial)
                {
                    var asphalt = DefDatabase<RoadDef>.GetNamed("AsphaltRoad", false)
                               ?? DefDatabase<RoadDef>.GetNamed("AncientAsphaltRoad", false);
                    if (asphalt != null) return asphalt;
                }
                // Fallthrough to Stone if tech too low or Asphalt missing
                var stone = DefDatabase<RoadDef>.GetNamed("StoneRoadBuilt", false)
                         ?? DefDatabase<RoadDef>.GetNamed("StoneRoad", false);
                if (stone != null) return stone;
            }

            // Tier 2: Stone
            if (tier == SettlementTier.T2)
            {
                var stone = DefDatabase<RoadDef>.GetNamed("StoneRoadBuilt", false)
                         ?? DefDatabase<RoadDef>.GetNamed("StoneRoad", false);
                if (stone != null) return stone;
            }

            // Tier 1 / Fallback: Dirt
            return DefDatabase<RoadDef>.GetNamed("DirtRoadBuilt", false)
                ?? DefDatabase<RoadDef>.GetNamed("DirtRoad", false)
                ?? DefDatabase<RoadDef>.AllDefs.OrderBy(d => d.priority).FirstOrDefault(d => d.defName.ToLower().Contains("road"));
        }

        private static RoadDef GetRoadDefForActor(WorldObject actor)
        {
            var comp = actor.GetComponent<CompViralSpread>();
            TechLevel factionTech = actor.Faction?.def.techLevel ?? TechLevel.Undefined;

            if (actor is Outpost)
                return GetRoadDefByTier(comp?.selectedRoadTier ?? SettlementTier.T1, factionTech);

            SettlementTier sTier = SettlementTier.T1;
            // Surgical change: Simplified tier extraction
            var tierField = actor.GetType().GetField("tier", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (tierField != null) sTier = (SettlementTier)tierField.GetValue(actor);

            return GetRoadDefByTier(sTier, factionTech);
        }
    }
}