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
        /// <summary>Routes a terminal argument to its handler.</summary>
        void DispatchCommand(string argument)
        {
            switch (argument.ToLower())
            {
                case "cleanup":
                    CleanupAllCustomData();
                    break;
                case "rescan":
                    RescanRequested = true;
                    _configDirty = true;
                    break;
                case "pause":
                    _paused = true;
                    break;
                case "resume":
                    _paused = false;
                    break;
                case "debug on":
                    DebugLogging = true;
                    break;
                case "debug off":
                    DebugLogging = false;
                    break;
            }
        }

        /// <summary>
        /// Renders pipeline status to the programmable block screen. The instruction high
        /// water mark specifically exists so a future report of a size-related failure can be
        /// diagnosed without profiling from scratch.
        /// </summary>
        void RenderEchoStatus()
        {
            Echo("Farmhand");
            Echo($"{Version} | {PublishedDate}");
            Echo(_paused ? "[PAUSED]" : "");
            Echo($"Step {_stepIndex}/{StepLabels.Length}: {_stepLabel}");
            Echo($"Cycle {CycleNumber} | {TicksLastCycle} ticks");
            Echo($"Instr {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}");
            Echo($"Peak {InstructionHighWater} (limit {(int)(Runtime.MaxInstructionCount * BudgetFraction)})");

            if (DebugLogging)
            {
                Echo("");
                Echo($"Groups: {farmGroups.GroupCount}");
                Echo($"PlotLCDs: {plotLcds.Count}");
                Echo($"Rescan in: {RescanIntervalCycles - (CycleNumber - _lastDiscoveryCycle)}");
            }
        }

        /// <summary>
        /// Prints header and diagnostic information to the programmable block screen
        /// </summary>
        void PrintDiagnosticHeader()
        {
            var header = RenderHelpers.GetHeaderAnimation(
                runNumber,
                "Farmhand",
                TextAlignment.LEFT
            );

            WriteToDiagnosticOutput(header, true);
            WriteToDiagnosticOutput($"{Version} | {PublishedDate}", true);
            WriteToDiagnosticOutput("", true);

            // Print diagnostic info once per cycle
            foreach (var farmGroup in farmGroups.GetAllGroups())
            {
                WriteToDiagnosticOutput($"Group: {farmGroup.GroupName}", true);
                if (farmGroup.FarmPlots.Count > 0)
                {
                    WriteToDiagnosticOutput($"Farm Plots: {farmGroup.FarmPlots.Count}");
                }
                if (farmGroup.IrrigationSystems.Count > 0)
                {
                    WriteToDiagnosticOutput(
                        $"Irrigation Systems: {farmGroup.IrrigationSystems.Count}"
                    );
                }
                if (farmGroup.WaterTanks.Count > 0)
                {
                    WriteToDiagnosticOutput($"Water Tanks: {farmGroup.WaterTanks.Count}");
                }
                var displayCount =
                    farmGroup.LcdPanels.Count + farmGroup.TextSurfaceProviders.Count;
                if (displayCount > 0)
                {
                    WriteToDiagnosticOutput($"LCD Screen Providers: {displayCount}");
                }
                if (farmGroup.AirVents.Count > 0)
                {
                    WriteToDiagnosticOutput($"Air Vents: {farmGroup.AirVents.Count}");
                }
                if (farmGroup.SolarFoodGenerators.Count > 0)
                {
                    WriteToDiagnosticOutput(
                        $"Solar Food Generators: {farmGroup.SolarFoodGenerators.Count}"
                    );
                }
                if (farmGroup.StateManager.RegisteredTimerCount > 0)
                {
                    WriteToDiagnosticOutput(
                        $"Timers: {farmGroup.StateManager.RegisteredTimerCount}"
                    );
                }
                if (farmGroup.StateManager.RegisteredActionRelayCount > 0)
                {
                    WriteToDiagnosticOutput(
                        $"Action Relays: {farmGroup.StateManager.RegisteredActionRelayCount}"
                    );
                }
                if (farmGroup.StateManager.RegisteredBroadcastControllerCount > 0)
                {
                    WriteToDiagnosticOutput(
                        $"Broadcast Controllers: {farmGroup.StateManager.RegisteredBroadcastControllerCount}"
                    );
                }
            }

            // Print PlotLCD count (independent of groups)
            if (plotLcds.Count > 0)
            {
                WriteToDiagnosticOutput($"Plot LCDs: {plotLcds.Count}");
            }
        }

        /// <summary>
        /// Cleans up obsolete custom data from all managed blocks
        /// </summary>
        void CleanupAllCustomData()
        {
            Echo("Starting custom data cleanup...");
            int blocksProcessed = 0;

            // Process all farm groups
            var allGroups = farmGroups.GetAllGroups();
            foreach (var farmGroup in allGroups)
            {
                // Clean farm plots
                foreach (var block in farmGroup.FarmPlots)
                {
                    block.CleanupCustomData();
                    blocksProcessed++;
                }

                // Clean irrigation systems
                foreach (var block in farmGroup.IrrigationSystems)
                {
                    block.CleanupCustomData();
                    blocksProcessed++;
                }

                // Clean water tanks
                foreach (var block in farmGroup.WaterTanks)
                {
                    block.CleanupCustomData();
                    blocksProcessed++;
                }

                // Clean LCD panels
                foreach (var block in farmGroup.LcdPanels)
                {
                    block.CleanupCustomData();
                    blocksProcessed++;
                }

                // Clean text surface providers
                foreach (var block in farmGroup.TextSurfaceProviders)
                {
                    block.CleanupCustomData();
                    blocksProcessed++;
                }

                // Clean air vents
                foreach (var block in farmGroup.AirVents)
                {
                    block.CleanupCustomData();
                    blocksProcessed++;
                }

                // Clean solar food generators
                foreach (var block in farmGroup.SolarFoodGenerators)
                {
                    block.CleanupCustomData();
                    blocksProcessed++;
                }

                // Clean timers
                foreach (var timer in farmGroup.StateManager.GetTimers())
                {
                    timer.CleanupCustomData();
                    blocksProcessed++;
                }

                // Clean action relays
                foreach (var relay in farmGroup.StateManager.GetActionRelays())
                {
                    relay.CleanupCustomData();
                    blocksProcessed++;
                }

                // Clean broadcast controllers
                foreach (var controller in farmGroup.StateManager.GetBroadcastControllers())
                {
                    controller.CleanupCustomData();
                    blocksProcessed++;
                }
            }

            // Clean programmable block
            thisPb.CleanupCustomData();
            blocksProcessed++;

            Echo($"Custom data cleanup complete!");
            Echo($"Processed {blocksProcessed} blocks.");
        }
    }
}
