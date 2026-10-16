using System.Collections.Generic;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using TSA_WorldDomination;
using TSA_WorldDomination.Settings;
using Event = UnityEngine.Event;
using EventType = UnityEngine.EventType;

namespace WorldDomination
{
    public class WorldLogisticsVisualizer : WorldComponent
    {
        private int cachedNetGeneration = int.MinValue;
        private readonly Dictionary<int, string> netLabelByObjectId = new Dictionary<int, string>(64);
        private readonly Dictionary<int, Color> netColorByObjectId = new Dictionary<int, Color>(64);
        private readonly Dictionary<long, string> foodLabelByKey = new Dictionary<long, string>(64);

        private static readonly Color ShadowColor = new Color(0, 0, 0, 0.7f);

        public WorldLogisticsVisualizer(World world) : base(world) { }

        public override void WorldComponentOnGUI()
        {
            base.WorldComponentOnGUI();

            if (Current.ProgramState != ProgramState.Playing || !WorldRendererUtility.WorldRendered) return;
            if (!WorldComponent_WDVisualizerToggle.ShowVisuals) return;
            if (WorldDominationMod.settings == null || !WorldDominationMod.settings.foodLogisticsActive) return;

            if (Event.current.type != EventType.Repaint) return;

            if (Find.WorldCameraDriver.AltitudePercent > 0.08f) return;

            var manager = Find.World.GetComponent<WorldComponent_LogisticsManager>();
            if (manager == null) return;

            var nodes = manager.GetCachedPlayerLogisticsNodes();
            if (nodes.Count == 0) return;

            if (cachedNetGeneration != manager.LogisticsNetDisplayGeneration)
            {
                cachedNetGeneration = manager.LogisticsNetDisplayGeneration;
                netLabelByObjectId.Clear();
                netColorByObjectId.Clear();
            }

            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;

            for (int i = 0; i < nodes.Count; i++)
            {
                var worldObject = nodes[i].Obj;
                var logi = nodes[i].Logi;
                if (worldObject == null || worldObject.Destroyed || logi == null) continue;
                if (!WorldObjectSelectionUtility.VisibleToCameraNow(worldObject)) continue;

                if (!netLabelByObjectId.TryGetValue(worldObject.ID, out string netLabel))
                {
                    float totalNet = manager.GetLogisticsNetDailyForOutpost(worldObject);
                    string netSign = totalNet >= 0 ? "+" : "";
                    netLabel = $"{netSign}{totalNet:F1}";
                    netLabelByObjectId[worldObject.ID] = netLabel;
                    netColorByObjectId[worldObject.ID] = totalNet > 0.1f ? Color.green : (totalNet < -0.1f ? Color.red : Color.yellow);
                }

                int foodRounded = Mathf.RoundToInt(logi.currentFood);
                long foodKey = ((long)worldObject.ID << 32) ^ (uint)foodRounded;
                if (!foodLabelByKey.TryGetValue(foodKey, out string foodLabel))
                {
                    foodLabel = $"{foodRounded}/100";
                    foodLabelByKey[foodKey] = foodLabel;
                    if (foodLabelByKey.Count > 512)
                        foodLabelByKey.Clear();
                }
                Color foodColor = logi.currentFood <= 20f ? Color.red : (logi.currentFood <= 50f ? Color.yellow : Color.white);
                Color netColor = netColorByObjectId[worldObject.ID];

                Vector2 screenPos = WorldObjectSelectionUtility.ScreenPos(worldObject);
                float yOffset = -45f;
                Rect netRect = new Rect(screenPos.x - 50f, screenPos.y + yOffset, 100f, 20f);
                Rect foodRect = new Rect(screenPos.x - 50f, screenPos.y + yOffset + 12f, 100f, 20f);

                GUI.color = ShadowColor;
                Widgets.Label(new Rect(netRect.x + 1f, netRect.y + 1f, netRect.width, netRect.height), netLabel);
                Widgets.Label(new Rect(foodRect.x + 1f, foodRect.y + 1f, foodRect.width, foodRect.height), foodLabel);

                GUI.color = netColor;
                Widgets.Label(netRect, netLabel);
                GUI.color = foodColor;
                Widgets.Label(foodRect, foodLabel);
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
        }
    }
}
