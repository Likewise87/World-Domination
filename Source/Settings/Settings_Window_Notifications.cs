using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace TSA_WorldDomination.Settings
{
    public class Dialog_NotificationSettings : Window
    {
        public override Vector2 InitialSize => new Vector2(1050f, 820f);

        public Dialog_NotificationSettings()
        {
            doCloseButton = true;
            forcePause = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Listing_Standard l = new Listing_Standard();
            l.Begin(inRect);
            var s = WorldDominationMod.settings;

            // --- SECTION 1: GLOBAL NOTIFICATIONS ---
            SettingsUI.DrawHeader(l, "TSA_WD_Notify_HeaderEvents".Translate());

            l.CheckboxLabeled(
                "TSA_WD_Notify_NewSettlement".Translate(),
                ref s.notifyNewSettlement,
                "TSA_WD_Notify_NewSettlementTooltip".Translate()
            );

            l.CheckboxLabeled(
                "TSA_WD_Notify_SettlementRaided".Translate(),
                ref s.notifySettlementRaided,
                "TSA_WD_Notify_SettlementRaidedTooltip".Translate()
            );

            l.CheckboxLabeled(
                "TSA_WD_Notify_OutpostDestroyed".Translate(),
                ref s.notifyOutpostDestroyed,
                "TSA_WD_Notify_OutpostDestroyedTooltip".Translate()
            );

            l.CheckboxLabeled(
                "TSA_WD_Notify_ThreatLevel".Translate(),
                ref s.notifyThreatLevel,
                "TSA_WD_Notify_ThreatLevelTooltip".Translate()
            );

            l.CheckboxLabeled(
                "TSA_WD_Notify_DiplomaticChange".Translate(),
                ref s.notifyDiplomaticChange,
                "TSA_WD_Notify_DiplomaticChangeTooltip".Translate()
            );

            l.CheckboxLabeled(
                "TSA_WD_Notify_BuffNerf".Translate(),
                ref s.notifyBuffNerf,
                "TSA_WD_Notify_BuffNerfTooltip".Translate()
            );

            // --- SECTION 2: RESET ---
            l.Gap(20f);
            if (l.ButtonText("TSA_WD_Notify_Reset".Translate()))
            {
                s.ResetNotifications();
            }

            l.End();
        }
    }
}