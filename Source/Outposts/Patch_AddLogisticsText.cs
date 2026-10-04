using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Outposts;

namespace TSA_WorldDomination
{
    [StaticConstructorOnStartup]
    public static class LogisticsTabInjector
    {
        static LogisticsTabInjector()
        {
            int injectedCount = 0;
            // Loop through every WorldObjectDef in the game
            foreach (WorldObjectDef def in DefDatabase<WorldObjectDef>.AllDefs)
            {
                // Check if the def uses the Outpost class (or any class deriving from it)
                if (typeof(Outpost).IsAssignableFrom(def.worldObjectClass))
                {
                    if (def.inspectorTabs == null) def.inspectorTabs = new List<Type>();

                    if (!def.inspectorTabs.Contains(typeof(WITab_Outpost_Logistics)))
                    {
                        def.inspectorTabs.Add(typeof(WITab_Outpost_Logistics));

                        // Force resolve the tab immediately so it shows up without a restart
                        if (def.inspectorTabsResolved == null) def.inspectorTabsResolved = new List<InspectTabBase>();
                        def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(typeof(WITab_Outpost_Logistics)));
                        injectedCount++;
                    }
                }
            }
            Log.Message($"[TSA Logistics] Injected Logistics Tab into {injectedCount} Outpost types.");
        }
    }
}