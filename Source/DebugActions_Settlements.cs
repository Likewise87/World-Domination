using Verse;
using RimWorld.Planet;
using WorldDomination;
using LudeonTK;
using RimWorld;
using System.Linq;
using Outposts; // This refers to the external VOE library
using TSA_WorldDomination.Outposts;

namespace TSA_WorldDomination.Debug
{
    public static class DebugActions_Settlements
    {
        /// <summary>
        /// WD1 equivalent of Experimental "Simulate Settlement Conquered here":
        /// click a tile → ensure ruins → open VOE outpost type picker with T3 recruit count.
        /// </summary>
        [DebugAction("World Domination", "Spawn VOE Outpost here",
            allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        public static void SpawnVoeOutpostAtTile()
        {
            Messages.Message(
                "WD debug: click a world tile to spawn a VOE outpost (creates ruins if empty, opens type picker).",
                MessageTypeDefOf.NeutralEvent);

            Find.WorldTargeter.BeginTargeting(
                (GlobalTargetInfo target) =>
                {
                    if (!target.IsValid || target.Tile < 0)
                        return false;

                    int tile = target.Tile;
                    if (Find.WorldObjects.AnyWorldObjectAt(tile))
                    {
                        // Allow ruins / empty-ish tiles; block if a live settlement or outpost is already there.
                        bool blocked = Find.WorldObjects.ObjectsAt(tile).Any(o =>
                            o is Settlement || o is Outpost);
                        if (blocked)
                        {
                            Messages.Message("WD debug: tile already has a settlement or outpost.", MessageTypeDefOf.RejectInput);
                            return true;
                        }
                    }

                    DestroyedSettlement ruins = Find.WorldObjects.ObjectsAt(tile)
                        .OfType<DestroyedSettlement>()
                        .FirstOrDefault();
                    if (ruins == null)
                    {
                        Faction npc = Find.FactionManager?.AllFactionsVisible?
                            .FirstOrDefault(f => !f.IsPlayer && !f.defeated && f.def != null && !f.Hidden);
                        ruins = (DestroyedSettlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.DestroyedSettlement);
                        ruins.Tile = tile;
                        if (npc != null) ruins.SetFaction(npc);
                        Find.WorldObjects.Add(ruins);
                    }

                    string siteName = ruins.LabelCap ?? "Debug Outpost";
                    Find.WindowStack.Add(new Dialog_OutpostSelection(
                        tile,
                        siteName,
                        ruins.ID,
                        SettlementTier.T3,
                        null));
                    Messages.Message($"WD debug: opened VOE outpost picker at tile {tile} ({siteName}, T3).", MessageTypeDefOf.PositiveEvent);
                    return true;
                },
                true,
                null,
                false,
                null,
                null,
                t => t.IsValid && t.Tile >= 0);
        }

        [DebugAction("World Domination", "Upgrade Tier (Click)",
            actionType = DebugActionType.ToolWorld,
            allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        public static void UpgradeSettlementTier()
        {
            int tile = GenWorld.MouseTile();
            // FIXED: Just use Outpost (since it's in the 'using' list) 
            // or use global::Outposts.Outpost if the collision persists.
            WorldObject obj = Find.WorldObjects.ObjectsAt(tile).FirstOrDefault(x => x is Settlement || x is Outpost);

            if (obj == null) return;

            var comp = obj.GetComponent<CompViralSpread>();
            if (comp == null || comp.subType == "Vassal") return;

            if (comp.tier == SettlementTier.T1) comp.SetState(SettlementTier.T2);
            else if (comp.tier == SettlementTier.T2) comp.SetState(SettlementTier.T3);
            else if (comp.tier == SettlementTier.T3) comp.SetState(SettlementTier.T4);

            comp.strength = CompViralSpread.GetStrengthRange(comp.tier).min;

            Messages.Message($"Debug: {obj.LabelCap} upgraded to {comp.tier}.", MessageTypeDefOf.CautionInput);
            Find.World.GetComponent<WorldComponent_SpreadManager>()?.Notify_WeightsChanged();
        }

        [DebugAction("World Domination", "Downgrade Tier (Click)",
            actionType = DebugActionType.ToolWorld,
            allowedGameStates = AllowedGameStates.PlayingOnWorld)]
        public static void DowngradeSettlementTier()
        {
            int tile = GenWorld.MouseTile();
            // FIXED: Reference changed here as well
            WorldObject obj = Find.WorldObjects.ObjectsAt(tile).FirstOrDefault(x => x is Settlement || x is Outpost);

            if (obj == null) return;

            var comp = obj.GetComponent<CompViralSpread>();
            if (comp == null || comp.subType == "Vassal") return;

            if (comp.tier == SettlementTier.T4) comp.SetState(SettlementTier.T3);
            else if (comp.tier == SettlementTier.T3) comp.SetState(SettlementTier.T2);
            else if (comp.tier == SettlementTier.T2) comp.SetState(SettlementTier.T1);

            comp.strength = CompViralSpread.GetStrengthRange(comp.tier).min;

            Messages.Message($"Debug: {obj.LabelCap} downgraded to {comp.tier}.", MessageTypeDefOf.CautionInput);
            Find.World.GetComponent<WorldComponent_SpreadManager>()?.Notify_WeightsChanged();
        }
    }
}