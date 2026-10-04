using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;
using Outposts;

namespace WorldDomination
{
    public class Window_FactionDetails : Window
    {
        private Faction faction;
        private Vector2 scrollPos;
        private List<WorldObject> entries; // Unified list for Settlements or Outposts
        public override Vector2 InitialSize => new Vector2(700f, 600f);

        public Window_FactionDetails(Faction faction)
        {
            this.faction = faction;
            this.doCloseX = true;
            this.draggable = true;

            // Handle Player (Outposts) vs NPC (Settlements)
            if (faction.IsPlayer)
            {
                this.entries = Find.WorldObjects.AllWorldObjects.OfType<Outpost>()
                    .Cast<WorldObject>()
                    .OrderByDescending(o => o.GetComponent<CompViralSpread>()?.strength ?? 0)
                    .ToList();
            }
            else
            {
                this.entries = Find.WorldObjects.Settlements
                    .Where(s => s.Faction == faction)
                    .Cast<WorldObject>()
                    .OrderByDescending(s => s.GetComponent<CompViralSpread>()?.strength ?? 0)
                    .ToList();
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 35f), "TSA_WD_FacDet_Title".Translate(faction.Name));
            Text.Font = GameFont.Small;

            float colTier = 60f;
            float colName = 280f; // Slightly narrowed to prevent overlap with buttons
            float colStr = 100f;
            float rowHeight = 30f;

            // --- Header ---
            Rect hRect = new Rect(0, 45f, inRect.width, 25f);
            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleLeft; // Align headers
            Widgets.Label(new Rect(0, hRect.y, colTier, 25f), "TSA_WD_FacDet_HeaderTier".Translate());
            Widgets.Label(new Rect(colTier, hRect.y, colName, 25f), "TSA_WD_FacDet_HeaderName".Translate());
            Widgets.Label(new Rect(colTier + colName, hRect.y, colStr, 25f), "TSA_WD_FacDet_HeaderStrength".Translate());
            GUI.color = Color.white;

            Rect viewRect = new Rect(0, 0, inRect.width - 25f, entries.Count * rowHeight);
            Widgets.BeginScrollView(new Rect(0, 75f, inRect.width, inRect.height - 90f), ref scrollPos, viewRect);

            for (int i = 0; i < entries.Count; i++)
            {
                var obj = entries[i];
                var comp = obj.GetComponent<CompViralSpread>();
                Rect row = new Rect(0, i * rowHeight, viewRect.width, rowHeight);

                if (i % 2 == 0) Widgets.DrawHighlight(row);

                // --- Tier logic (Updated for T4) ---
                string tierDisplay = "??";
                if (!faction.IsPlayer)
                {
                    tierDisplay = comp?.tier.ToString() ?? "??";
                }
                else
                {
                    float s = comp?.strength ?? 0f;
                    if (s > 2000f) tierDisplay = "T4";
                    else if (s > 1000f) tierDisplay = "T3";
                    else if (s > 500f) tierDisplay = "T2";
                    else tierDisplay = "T1";
                }

                // --- Center Alignment ---
                Text.Anchor = TextAnchor.MiddleLeft;

                // Draw Labels
                Widgets.Label(new Rect(row.x + 5, row.y, colTier, rowHeight), tierDisplay);

                // REMOVED: if (tierDisplay.Contains("4")) GUI.color = Color.magenta;
                // The name will now draw in the default color (White or Faction Color if you applied it elsewhere)
                Widgets.Label(new Rect(row.x + colTier, row.y, colName, rowHeight), obj.LabelCap.Truncate(colName));

                Widgets.Label(new Rect(row.x + colTier + colName, row.y, colStr, rowHeight), comp?.strength.ToString("F0") ?? "0");

                // --- Button (Kept aligned to right) ---
                Rect goBtn = new Rect(row.xMax - 110f, row.y + 3f, 85f, rowHeight - 6f);
                if (Widgets.ButtonText(goBtn, "TSA_WD_FacDet_BtnView".Translate()))
                {
                    CameraJumper.TryJumpAndSelect(obj);
                    this.Close();
                    Find.WindowStack.WindowOfType<Window_WorldStats>()?.Close();
                }
            }

            // Reset Anchor so other UI elements don't break
            Text.Anchor = TextAnchor.UpperLeft;
            Widgets.EndScrollView();
        }
    }
}