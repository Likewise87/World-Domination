using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace WorldDomination
{
    public static class WorldDomination_UIUtils
    {
        /// <summary>
        /// Draws a faction icon tinted with the faction's custom color.
        /// Extracts faction automatically from GlobalTargetInfo.
        /// </summary>
        public static void DrawFactionIconWithColor(Rect rect, GlobalTargetInfo target)
        {
            Faction faction = null;

            if (target.HasWorldObject)
                faction = target.WorldObject.Faction;
            else if (target.HasThing)
                faction = target.Thing.Faction;

            if (faction != null)
            {
                DrawFactionIconWithColor(rect, faction);
            }
        }

        /// <summary>
        /// Draws a faction icon tinted with the faction's custom color.
        /// Directly accepts a Faction object.
        /// </summary>
        public static void DrawFactionIconWithColor(Rect rect, Faction faction)
        {
            if (faction?.def?.FactionIcon != null)
            {
                // GUI.DrawTexture with the color parameter is the most reliable way to tint
                // It bypasses the button logic that often resets GUI.color
                GUI.DrawTexture(rect, faction.def.FactionIcon, ScaleMode.ScaleToFit, true, 0f, faction.Color, 0f, 0f);
            }
        }
    }
}