using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace TSA_WorldDomination
{
    [HarmonyPatch(typeof(Dialog_FactionDuringLanding), "DoWindowContents")]
    public static class DiplomacyUIHooks
    {
        private const float ButtonWidth = 180f;
        private const float ButtonHeight = 30f;
        private const float ButtonGap = 10f;

        public static void Postfix(Rect inRect)
        {
            // Positioned at the top of the window to avoid overlap with 'Close'
            float xPos = inRect.x;
            float yPos = inRect.y + 10f;

            // 1. Randomize Allegiances
            Rect rect1 = new Rect(xPos, yPos, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(rect1, "TSA_WD_Button_Randomize".Translate()))
            {
                RandomizeAllDiplomacy();
            }

            // 2. Total Hostility
            xPos += ButtonWidth + ButtonGap;
            Rect rect2 = new Rect(xPos, yPos, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(rect2, "TSA_WD_Button_AllHostile".Translate()))
            {
                MakeAllHostileToPlayer();
            }

            // 3. Reset to Defaults
            xPos += ButtonWidth + ButtonGap;
            Rect rect3 = new Rect(xPos, yPos, ButtonWidth, ButtonHeight);
            if (Widgets.ButtonText(rect3, "TSA_WD_Button_Reset".Translate()))
            {
                ResetToDefaults();
            }
        }

        private static void RandomizeAllDiplomacy()
        {
            var factions = Find.FactionManager.AllFactionsVisibleInViewOrder.ToList();
            for (int i = 0; i < factions.Count; i++)
            {
                for (int j = i + 1; j < factions.Count; j++)
                {
                    Faction a = factions[i];
                    Faction b = factions[j];

                    // OVERRIDE: We allow randomizing even permanent enemies here 
                    float rand = Rand.Value;
                    FactionRelationKind next = rand < 0.33f ? FactionRelationKind.Hostile :
                                               (rand < 0.66f ? FactionRelationKind.Neutral : FactionRelationKind.Ally);

                    ApplySurgicalRelation(a, b, next);
                }
            }
            Messages.Message("TSA_WD_Msg_Randomized".Translate(), MessageTypeDefOf.CautionInput, false);
        }

        private static void MakeAllHostileToPlayer()
        {
            Faction player = Faction.OfPlayer;
            foreach (var faction in Find.FactionManager.AllFactionsVisible)
            {
                if (faction != player)
                {
                    ApplySurgicalRelation(player, faction, FactionRelationKind.Hostile);
                }
            }
            Messages.Message("TSA_WD_Msg_AllHostile".Translate(), MessageTypeDefOf.NegativeEvent, false);
        }

        private static void ResetToDefaults()
        {
            var factions = Find.FactionManager.AllFactions.ToList();
            for (int i = 0; i < factions.Count; i++)
            {
                for (int j = i + 1; j < factions.Count; j++)
                {
                    Faction a = factions[i];
                    Faction b = factions[j];

                    int goodwillA = GetDefaultGoodwill(a, b);
                    int goodwillB = GetDefaultGoodwill(b, a);
                    int finalGoodwill = Mathf.Min(goodwillA, goodwillB);

                    FactionRelationKind kind;
                    if (finalGoodwill <= -10) kind = FactionRelationKind.Hostile;
                    else if (finalGoodwill >= 75) kind = FactionRelationKind.Ally;
                    else kind = FactionRelationKind.Neutral;

                    ApplySurgicalRelation(a, b, kind, finalGoodwill);
                }
            }
            Messages.Message("TSA_WD_Msg_Reset".Translate(), MessageTypeDefOf.PositiveEvent, false);
        }

        private static int GetDefaultGoodwill(Faction a, Faction b)
        {
            if (a.def.permanentEnemy) return -100;
            if (a.def.permanentEnemyToEveryoneExceptPlayer && !b.IsPlayer) return -100;
            if (a.def.permanentEnemyToEveryoneExcept != null && !a.def.permanentEnemyToEveryoneExcept.Contains(b.def)) return -100;
            if (a.def.naturalEnemy) return -80;
            return 0;
        }

        private static void ApplySurgicalRelation(Faction facA, Faction facB, FactionRelationKind next, int? specificGoodwill = null)
        {
            int targetGoodwill = specificGoodwill ?? ((next == FactionRelationKind.Hostile) ? -80 : (next == FactionRelationKind.Ally) ? 80 : 0);

            FactionRelation relA = facA.RelationWith(facB, false);
            relA.baseGoodwill = targetGoodwill;
            relA.kind = next;

            FactionRelation relB = facB.RelationWith(facA, false);
            relB.baseGoodwill = targetGoodwill;
            relB.kind = next;

            bool flag;
            relA.CheckKindThresholds(facA, false, null, GlobalTargetInfo.Invalid, out flag);
            relB.CheckKindThresholds(facB, false, null, GlobalTargetInfo.Invalid, out flag);
        }
    }
}