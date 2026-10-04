using Verse;
using RimWorld.Planet;
using WorldDomination;
using LudeonTK;
using RimWorld;
using System.Linq;
using Outposts; // This refers to the external VOE library

namespace TSA_WorldDomination.Debug
{
    public static class DebugActions_Settlements
    {
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