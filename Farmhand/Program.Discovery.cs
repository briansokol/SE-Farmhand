using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    public partial class Program
    {
        /// <summary>
        /// Rediscovers tagged displays and group members when discovery is due.
        /// Exits immediately on cycles where nothing needs rescanning.
        /// </summary>
        IEnumerator<YieldReason> StepDiscoveryIfDue()
        {
            if (!IsDiscoveryDue())
            {
                yield return YieldReason.ChunkBoundary;
                yield break;
            }

            IEnumerator<YieldReason> farmLcds = FindFarmLCDBlocks();
            while (farmLcds.MoveNext())
            {
                yield return farmLcds.Current;
            }
            yield return YieldReason.ChunkBoundary;
            if (BudgetExceeded()) yield return YieldReason.BudgetHit;

            FindPlotLCDBlocks();
            MarkDiscoveryDone();

            // Discovery may have added or removed groups, and StepRoot took its snapshot
            // before this step ran. Refreshing here keeps the rest of the cycle on the real
            // group set: a stale entry for a removed group would be resurrected as a phantom
            // when WriteToMainOutput calls GetGroup, which creates on miss, and a newly
            // discovered group would otherwise go unscanned until the following cycle.
            RefreshGroupSnapshot();

            yield return YieldReason.ChunkBoundary;
        }

        /// <summary>
        /// Discovers and categorizes blocks with [FarmLCD] tags for farm management.
        /// Yields between each per-group block search: run as one unit this measured 7,300
        /// instructions, matching BuildText as the script's worst-case chunk. Nothing else
        /// runs while discovery is in progress, so the group set being half rebuilt across
        /// a tick boundary is never observed by another step.
        /// </summary>
        IEnumerator<YieldReason> FindFarmLCDBlocks()
        {
            var lcdPanels = new List<LcdPanel>();
            var surfaceProviders = new List<TextSurfaceProvider>();

            // Find the blocks with [FarmLCD] in their custom name
            List<IMyTerminalBlock> lcdTaggedBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName($"[{lcdTag}]", lcdTaggedBlocks);

            lcdTaggedBlocks.ForEach(block =>
            {
                if (TextSurfaceProvider.BlockIsValid(block))
                {
                    TextSurfaceProvider provider;
                    if (!_textSurfaceProvidersById.TryGetValue(block.EntityId, out provider))
                    {
                        provider = new TextSurfaceProvider(block, this, shiftSprites);
                        _textSurfaceProvidersById[block.EntityId] = provider;
                    }
                    else
                    {
                        // Reused wrapper: the constructor's UpdateCustomData no longer runs
                        // every cycle, so restore a config key the player deleted here.
                        provider.UpdateCustomData();
                    }
                    surfaceProviders.Add(provider);
                }
                else if (LcdPanel.BlockIsValid(block as IMyFunctionalBlock))
                {
                    LcdPanel lcdPanel;
                    if (!_lcdPanelsById.TryGetValue(block.EntityId, out lcdPanel))
                    {
                        lcdPanel = new LcdPanel(block as IMyTextPanel, this, shiftSprites);
                        _lcdPanelsById[block.EntityId] = lcdPanel;
                    }
                    else
                    {
                        lcdPanel.UpdateCustomData();
                    }
                    lcdPanels.Add(lcdPanel);
                }
            });

            var groupNames = lcdPanels
                .ConvertAll(panel => panel.GroupName())
                .FindAll(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToList();

            var surfaceProviderGroupNames = surfaceProviders
                .ConvertAll(provider => provider.GroupName())
                .FindAll(name => !string.IsNullOrWhiteSpace(name));

            groupNames.AddRange(surfaceProviderGroupNames);
            groupNames = groupNames.Distinct().ToList();

            // Get group name from this programmable block if set
            var pbGroupName = thisPb.GroupName();
            if (!string.IsNullOrWhiteSpace(pbGroupName) && !groupNames.Contains(pbGroupName))
            {
                groupNames.Add(pbGroupName);
            }

            // Remove those farm groups that are no longer referenced by any LCD panel or this programmable block
            farmGroups.RemoveGroupsNotInList(groupNames);
            yield return YieldReason.ChunkBoundary;

            // For each group name, find and register the blocks. Each Find call below walks
            // the grid, so they are separated by chunk boundaries rather than run as a batch.
            foreach (var groupName in groupNames)
            {
                var lcdPanelsInGroup = lcdPanels.FindAll(panel => panel.GroupName() == groupName);
                var surfaceProvidersInGroup = surfaceProviders.FindAll(provider =>
                    provider.GroupName() == groupName
                );
                farmGroups.ResetBlocks(groupName, lcdPanelsInGroup, surfaceProvidersInGroup);

                var group = farmGroups.GetGroup(groupName);
                group.ProgrammableBlock = thisPb;
                yield return YieldReason.ChunkBoundary;

                farmGroups.FindFarmPlots(groupName);
                yield return YieldReason.ChunkBoundary;
                farmGroups.FindIrrigationSystems(groupName);
                yield return YieldReason.ChunkBoundary;
                farmGroups.FindWaterTanks(groupName);
                yield return YieldReason.ChunkBoundary;
                farmGroups.FindAirVents(groupName);
                yield return YieldReason.ChunkBoundary;
                farmGroups.FindSolarFoodGenerators(groupName);
                yield return YieldReason.ChunkBoundary;
                farmGroups.FindTimers(groupName);
                yield return YieldReason.ChunkBoundary;
                farmGroups.FindActionRelays(groupName);
                yield return YieldReason.ChunkBoundary;
                farmGroups.FindBroadcastControllers(groupName);
                yield return YieldReason.ChunkBoundary;
            }
        }

        /// <summary>
        /// Discovers and categorizes blocks with [PlotLCD] tags
        /// </summary>
        void FindPlotLCDBlocks()
        {
            // Find blocks with [PlotLCD] in their custom name
            plotLcds.Clear();
            List<IMyTerminalBlock> plotLcdTaggedBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName($"[{plotLcdTag}]", plotLcdTaggedBlocks);

            plotLcdTaggedBlocks.ForEach(block =>
            {
                if (PlotLCD.BlockIsValid(block))
                {
                    PlotLCD plotLcd;
                    if (!_plotLcdsById.TryGetValue(block.EntityId, out plotLcd))
                    {
                        plotLcd = new PlotLCD(block as IMyTextPanel, this, shiftSprites);
                        _plotLcdsById[block.EntityId] = plotLcd;
                    }
                    plotLcds.Add(plotLcd);

                    // Find nearby farm plot (only if resolution is correct)
                    if (plotLcd.IsCorrectResolution)
                    {
                        plotLcd.FindNearbyFarmPlot();
                    }
                }
            });
        }
    }
}
