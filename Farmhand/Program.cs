using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace IngameScript
{
    /// <summary>
    /// Main Space Engineers Programmable Block Script for automated farm management
    /// </summary>
    public partial class Program : MyGridProgram
    {
        readonly FarmGroups farmGroups;
        readonly ProgrammableBlock thisPb;

        readonly string lcdTag = "FarmLCD";
        int runNumber = 0;
        readonly string Version = "v0.8.0";
        readonly string PublishedDate = "2025-10-17";

        // Coroutine management
        readonly List<IEnumerator<bool>> activeCoroutines = new List<IEnumerator<bool>>();
        double currentCycleTime = 0;
        double lastCycleTime = 0;

        public Program()
        {
            thisPb = new ProgrammableBlock(Me, this);
            farmGroups = new FarmGroups(GridTerminalSystem, this);
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        /// <summary>
        /// Required by Space Engineers API - currently not used
        /// </summary>
        public void Save() { }

        /// <summary>
        /// Main execution loop called every 10 ticks by Space Engineers
        /// </summary>
        public void Main()
        {
            // Start new coroutines immediately when no operations are running
            if (activeCoroutines.Count == 0)
            {
                RestartCoroutineCycle();
            }

            // Execute coroutines
            ExecuteCoroutines();

            // Accumulate cycle time
            currentCycleTime += Runtime.TimeSinceLastRun.TotalMilliseconds;

            Echo($"Instructions: {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}");
            Echo(
                $"Quota: {(float)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount:P2}"
            );
            Echo($"Active Coroutines: {activeCoroutines.Count}");
            Echo($"Last Cycle Time: {lastCycleTime / 1000:F2}s");
        }

        /// <summary>
        /// Executes active coroutines sequentially and manages their lifecycle
        /// </summary>
        void ExecuteCoroutines()
        {
            if (activeCoroutines.Count == 0)
                return;

            // Always execute the first coroutine in the list
            var coroutine = activeCoroutines[0];
            try
            {
                if (!coroutine.MoveNext())
                {
                    // Coroutine completed, dispose it
                    coroutine.Dispose();
                    activeCoroutines.RemoveAt(0);

                    // Start the next coroutine in sequence
                    if (activeCoroutines.Count == 0)
                    {
                        RestartCoroutineCycle();
                    }
                }
            }
            catch
            {
                // Error in coroutine, dispose and skip to next
                coroutine.Dispose();
                activeCoroutines.RemoveAt(0);

                // Start the next coroutine in sequence
                if (activeCoroutines.Count == 0)
                {
                    RestartCoroutineCycle();
                }
            }
        }

        /// <summary>
        /// Restarts the coroutine cycle and prints diagnostic information
        /// </summary>
        void RestartCoroutineCycle()
        {
            // Save the completed cycle time and reset for new cycle
            lastCycleTime = currentCycleTime;
            currentCycleTime = 0;

            // All coroutines completed, restart the cycle
            PrintDiagnosticHeader();

            activeCoroutines.Add(FindBlocksCoroutine());
            activeCoroutines.Add(PrintHeaderCoroutine());
            activeCoroutines.Add(GetBlockStateCoroutine());
            activeCoroutines.Add(PrintOutputCoroutine());
        }

        /// <summary>
        /// Prints header and diagnostic information to the programmable block screen
        /// </summary>
        void PrintDiagnosticHeader()
        {
            var header = GetHeaderAnimation(runNumber);

            if (runNumber >= 2)
            {
                runNumber = 0;
            }
            else
            {
                runNumber += 1;
            }

            WriteToDiagnosticOutput(header);
            WriteToDiagnosticOutput($"{Version} | ({PublishedDate})");
            WriteToDiagnosticOutput("");

            // Print diagnostic info once per cycle
            farmGroups
                .GetAllGroups()
                .ForEach(farmGroup =>
                {
                    WriteToDiagnosticOutput($"Group: {farmGroup.GroupName}");
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
                    if (farmGroup.LcdPanels.Count > 0)
                    {
                        WriteToDiagnosticOutput($"LCD Panels: {farmGroup.LcdPanels.Count}");
                    }
                    if (farmGroup.Cockpits.Count > 0)
                    {
                        WriteToDiagnosticOutput($"Control Seats: {farmGroup.Cockpits.Count}");
                    }
                    if (farmGroup.AirVents.Count > 0)
                    {
                        WriteToDiagnosticOutput($"Air Vents: {farmGroup.AirVents.Count}");
                    }
                    if (farmGroup.StateManager.RegisteredTimerCount > 0)
                    {
                        WriteToDiagnosticOutput(
                            $"Timers: {farmGroup.StateManager.RegisteredTimerCount}"
                        );
                    }
                });
        }

        /// <summary>
        /// Prints header to LCD panels and cockpits (coroutine version)
        /// </summary>
        IEnumerator<bool> PrintHeaderCoroutine()
        {
            farmGroups
                .GetAllGroups()
                .ForEach(farmGroup =>
                {
                    WriteToMainOutput(
                        farmGroup.GroupName,
                        "Farmhand",
                        "Header",
                        isHeader: true,
                        runNumber: runNumber
                    );
                    WriteToMainOutput(farmGroup.GroupName, "", "Header", isHeader: true);
                });

            yield return true;
        }

        /// <summary>
        /// Discovers and categorizes blocks with [FarmLCD] tag for farm management (coroutine version)
        /// </summary>
        IEnumerator<bool> FindBlocksCoroutine()
        {
            var lcdPanels = new List<LcdPanel>();
            var cockpits = new List<Cockpit>();

            // Find the blocks with [FarmLCD] in their custom name
            List<IMyTerminalBlock> lcdTaggedBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName($"[{lcdTag}]", lcdTaggedBlocks);

            yield return true; // Yield after grid search

            lcdTaggedBlocks.ForEach(block =>
            {
                if (Cockpit.BlockIsValid(block))
                {
                    cockpits.Add(new Cockpit(block as IMyCockpit, this));
                }
                else if (LcdPanel.BlockIsValid(block as IMyFunctionalBlock))
                {
                    lcdPanels.Add(new LcdPanel(block as IMyTextPanel, this));
                }
            });

            var groupNames = lcdPanels
                .ConvertAll(panel => panel.GroupName())
                .FindAll(name => !string.IsNullOrWhiteSpace(name))
                .Distinct()
                .ToList();

            var cockpitGroupNames = cockpits
                .ConvertAll(cockpit => cockpit.GroupName())
                .FindAll(name => !string.IsNullOrWhiteSpace(name));

            groupNames.AddRange(cockpitGroupNames);
            groupNames = groupNames.Distinct().ToList();

            // Get group name from this programmable block if set
            var pbGroupName = thisPb.GroupName();
            if (!string.IsNullOrWhiteSpace(pbGroupName) && !groupNames.Contains(pbGroupName))
            {
                groupNames.Add(pbGroupName);
            }

            // Remove those farm groups that are no longer referenced by any LCD panel or this programmable block
            farmGroups.RemoveGroupsNotInList(groupNames);

            yield return true; // Yield after removing old groups

            // For each group name, find and register the blocks
            foreach (var groupName in groupNames)
            {
                var lcdPanelsInGroup = lcdPanels.FindAll(panel => panel.GroupName() == groupName);
                var cockpitsInGroup = cockpits.FindAll(cockpit => cockpit.GroupName() == groupName);
                farmGroups.ResetBlocks(groupName, lcdPanelsInGroup, cockpitsInGroup);

                var group = farmGroups.GetGroup(groupName);
                group.ProgrammableBlock = thisPb;

                farmGroups.FindFarmPlots(groupName);

                // Wait a tick if there are more than 50 farm plots to process
                if (group.FarmPlots.Count > 50)
                {
                    yield return true;
                }

                farmGroups.FindIrrigationSystems(groupName);
                farmGroups.FindAirVents(groupName);
                farmGroups.FindTimers(groupName);

                // Wait a tick if there are more than 2 groups to process
                if (groupNames.Count > 2)
                {
                    yield return true;
                }
            }
        }

        /// <summary>
        /// Updates state of all farm blocks and prepares output messages (coroutine version)
        /// </summary>
        IEnumerator<bool> GetBlockStateCoroutine()
        {
            var allGroups = farmGroups.GetAllGroups();

            foreach (var farmGroup in allGroups)
            {
                var groupName = farmGroup.GroupName;

                // Initialize stats for this farm group
                var stats = new FarmStats();

                // Check farm plots
                if (farmGroup.FarmPlots.Count > 0)
                {
                    farmGroup.FarmPlots.ForEach(farmPlot =>
                    {
                        var plantType = farmPlot.PlantType;
                        var plantYield = farmPlot.PlantYieldAmount;
                        var plotDetails = farmPlot.GetPlotDetails();

                        stats.WaterUsagePerMinute += plotDetails.WaterUsage;

                        if (farmPlot.IsPlantPlanted)
                        {
                            if (farmPlot.IsPlantAlive)
                            {
                                // Set the plot plant count
                                stats.PlotSummary[plantType] = stats.PlotSummary.ContainsKey(
                                    plantType
                                )
                                    ? stats.PlotSummary[plantType] + 1
                                    : 1;

                                if (plotDetails.CropHealth < thisPb.HealthLowThreshold)
                                {
                                    stats.AlertMessages.Add(
                                        $"Health Low: {plotDetails.CropHealth:P1} ({farmPlot.PlantType}, {farmPlot.CustomName})"
                                    );
                                    stats.DyingPlants++;
                                }

                                if (farmPlot.IsPlantFullyGrown)
                                {
                                    // Plant is ready to harvest
                                    farmPlot.SetLightColor(thisPb.PlantedReadyColor);
                                    stats.FarmPlotsReadyToHarvest++;

                                    // Set the yield summary
                                    stats.YieldSummary[plantType] = stats.YieldSummary.ContainsKey(
                                        plantType
                                    )
                                        ? stats.YieldSummary[plantType] + plantYield
                                        : plantYield;
                                }
                                else
                                {
                                    // Plant is still growing
                                    farmPlot.SetLightColor(thisPb.PlantedAliveColor);

                                    if (
                                        !stats.GrowthSummary.ContainsKey(plantType)
                                        || (
                                            plotDetails.GrowthProgress
                                                > stats.GrowthSummary[plantType]
                                            && plotDetails.GrowthProgress < 1f
                                        )
                                    )
                                    {
                                        stats.GrowthSummary[plantType] = plotDetails.GrowthProgress;
                                    }
                                }
                            }
                            else
                            {
                                // Plant is dead
                                farmPlot.SetLightColor(thisPb.PlantedDeadColor);
                                stats.DeadPlants++;
                                stats.CausesOfDeath.Add(
                                    string.IsNullOrWhiteSpace(plotDetails.CauseOfDeath)
                                        ? "Unknown"
                                        : plotDetails.CauseOfDeath
                                );
                            }
                        }
                        else
                        {
                            // No plant
                            farmPlot.SetLightColor(thisPb.PlanterEmptyColor);
                            stats.SeedsNeeded += farmPlot.SeedsNeeded;
                        }

                        if (
                            farmPlot.IsFunctional()
                            && farmPlot.WaterFilledRatio <= thisPb.WaterLowThreshold
                        )
                        {
                            farmPlot.LightBlinkInterval = 1f;
                            farmPlot.LightBlinkLength = 50f;
                            stats.AlertMessages.Add(
                                $"Water Low: {farmPlot.WaterFilledRatio:P1} ({farmPlot.PlantType}, {farmPlot.CustomName})"
                            );
                            stats.FarmPlotsLowOnWater++;
                        }
                        else
                        {
                            farmPlot.LightBlinkInterval = 0f;
                            farmPlot.LightBlinkLength = 1f;
                        }
                    });

                    farmGroup.StateManager.UpdateState("OnCropDying", stats.DyingPlants > 0);
                    farmGroup.StateManager.UpdateState("OnWaterLow", stats.FarmPlotsLowOnWater > 0);

                    // Check air vents
                    if (farmGroup.AirVents.Count > 0)
                    {
                        var vent = farmGroup.AirVents[0];
                        stats.OxygenLevel = vent.OxygenLevel;

                        switch (vent.Status)
                        {
                            case VentStatus.Pressurizing:
                            case VentStatus.Pressurized:
                                stats.VentStatusText = $"Pressurized: {vent.OxygenLevel:P0}";
                                stats.IsPressurized = true;
                                farmGroup.StateManager.UpdateState("OnPressurized", true);
                                break;
                            case VentStatus.Depressurizing:
                            case VentStatus.Depressurized:
                                if (vent.CanPressurize)
                                {
                                    stats.VentStatusText =
                                        $"Depressurized (Room is Air Tight): {vent.OxygenLevel:P0}";
                                }
                                else
                                {
                                    stats.VentStatusText = $"Depressurized: {vent.OxygenLevel:P0}";
                                }
                                stats.IsPressurized = false;
                                farmGroup.StateManager.UpdateState("OnPressurized", false);
                                break;
                        }
                    }

                    // Check irrigation systems
                    if (farmGroup.IrrigationSystems.Count > 0)
                    {
                        float iceVolume = 0f;
                        float inventoryVolume = 0f;

                        farmGroup.IrrigationSystems.ForEach(irrigationSystem =>
                        {
                            iceVolume += irrigationSystem.CurrentVolume;
                            inventoryVolume += irrigationSystem.MaxVolume;
                        });

                        var iceLowThreshold = thisPb.IceLowThreshold;
                        stats.IceRatio = inventoryVolume > 0 ? iceVolume / inventoryVolume : 0f;
                        stats.CurrentIceKg = iceVolume / 0.37f;
                        stats.MaxIceKg = inventoryVolume / 0.37f;

                        farmGroup.StateManager.UpdateState(
                            "OnIceLow",
                            stats.IceRatio < iceLowThreshold
                        );
                    }

                    // Update state manager with stats
                    farmGroup.StateManager.UpdateState("OnCropDead", stats.DeadPlants > 0);
                    farmGroup.StateManager.UpdateState("OnCropAvailable", stats.SeedsNeeded > 0);
                    farmGroup.StateManager.UpdateState(
                        "OnCropReady",
                        stats.FarmPlotsReadyToHarvest > 0
                    );

                    // Check if all planted crops are ready to harvest
                    farmGroup.FarmPlots.ForEach(farmPlot =>
                    {
                        if (farmPlot.IsPlantPlanted && farmPlot.IsPlantAlive)
                        {
                            stats.TotalPlantedPlots++;
                        }
                    });

                    farmGroup.StateManager.UpdateState(
                        "OnAllCropsReady",
                        stats.TotalPlantedPlots > 0
                            && stats.FarmPlotsReadyToHarvest == stats.TotalPlantedPlots
                    );
                }

                // Store stats in the farm group
                farmGroup.Stats = stats;

                // Build text messages from stats for text-mode displays
                var farmPlotMessages = new List<string>();
                var atmosphereMessages = new List<string>();
                var irrigationMessages = new List<string>();
                var yieldMessages = new List<string>();

                if (stats.DeadPlants > 0)
                {
                    farmPlotMessages.Add(
                        $"Dead Plants: {stats.DeadPlants} ({string.Join(", ", stats.CausesOfDeath.Distinct())})"
                    );
                }

                if (stats.SeedsNeeded > 0)
                {
                    farmPlotMessages.Add($"Available Plots: {stats.SeedsNeeded}");
                }

                if (stats.FarmPlotsReadyToHarvest > 0)
                {
                    farmPlotMessages.Add($"Harvest Ready Plots: {stats.FarmPlotsReadyToHarvest}");
                }

                if (stats.WaterUsagePerMinute > 0f)
                {
                    farmPlotMessages.Add($"Water Usage: {stats.WaterUsagePerMinute:F1} L/min");
                }

                if (!string.IsNullOrWhiteSpace(stats.VentStatusText))
                {
                    atmosphereMessages.Add(stats.VentStatusText);
                }

                if (farmGroup.IrrigationSystems.Count > 0)
                {
                    irrigationMessages.Add(
                        $"Ice: {stats.IceRatio:P0} ({stats.CurrentIceKg:F1} kg / {stats.MaxIceKg:F1} kg)"
                    );
                }

                // Yield summary
                if (stats.PlotSummary.Count > 0)
                {
                    foreach (KeyValuePair<string, int> entry in stats.PlotSummary)
                    {
                        int plantYield = stats.YieldSummary.ContainsKey(entry.Key)
                            ? stats.YieldSummary[entry.Key]
                            : 0;
                        float growthProgress = stats.GrowthSummary.ContainsKey(entry.Key)
                            ? stats.GrowthSummary[entry.Key]
                            : 0f;

                        var yieldText = new List<string>();
                        if (growthProgress > 0f)
                        {
                            yieldText.Add($"{growthProgress:P1}");
                        }
                        if (plantYield > 0)
                        {
                            yieldText.Add($"{plantYield} Ready");
                        }

                        yieldMessages.Add(
                            $"{entry.Key} ({entry.Value} Plot{(entry.Value == 1 ? "" : "s")}): {string.Join(", ", yieldText)}"
                        );
                    }
                }

                if (stats.AlertMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Alerts", "ShowAlerts", isHeader: true);
                    stats.AlertMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowAlerts")
                    );
                    WriteToMainOutput(groupName, "", "ShowAlerts");
                }

                if (farmPlotMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Farm Plots", "ShowFarmPlots", isHeader: true);
                    farmPlotMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowFarmPlots")
                    );
                    WriteToMainOutput(groupName, "", "ShowFarmPlots");
                }

                if (atmosphereMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Atmosphere", "ShowAtmosphere", isHeader: true);
                    atmosphereMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowAtmosphere")
                    );
                    WriteToMainOutput(groupName, "", "ShowAtmosphere");
                }

                if (irrigationMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Irrigation", "ShowIrrigation", isHeader: true);
                    irrigationMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowIrrigation")
                    );
                    WriteToMainOutput(groupName, "", "ShowIrrigation");
                }

                if (yieldMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Current Yield", "ShowYield", isHeader: true);
                    yieldMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowYield")
                    );
                    WriteToMainOutput(groupName, "", "ShowYield");
                }

                yield return true; // Yield after processing each farm group
            }
        }

        /// <summary>
        /// Flushes accumulated text output to all LCD panels and cockpit screens (coroutine version)
        /// </summary>
        IEnumerator<bool> PrintOutputCoroutine()
        {
            thisPb.FlushTextToScreen();

            var allGroups = farmGroups.GetAllGroups();
            foreach (var farmGroup in allGroups)
            {
                farmGroup.LcdPanels.ForEach(panel =>
                {
                    panel.SetFarmGroup(farmGroup);
                    panel.FlushTextToScreen();
                });
                farmGroup.Cockpits.ForEach(cockpit =>
                {
                    cockpit.SetFarmGroup(farmGroup);
                    cockpit.FlushTextToScreens();
                });
            }

            yield return true;
        }

        /// <summary>
        /// Writes text to all LCD panels and cockpits in the specified farm group
        /// </summary>
        /// <param name="groupName">Name of the farm group to write to</param>
        /// <param name="text">Text content to display</param>
        /// <param name="category">Optional category for filtering display</param>
        /// <param name="isHeader">Whether this text is a header (headers are not indented)</param>
        /// <param name="runNumber">Animation frame number for animated header (0-2)</param>
        void WriteToMainOutput(
            string groupName,
            string text,
            string category = null,
            bool isHeader = false,
            int runNumber = 0
        )
        {
            var group = farmGroups.GetGroup(groupName);

            group.LcdPanels.ForEach(panel =>
            {
                panel.AppendText(text, category, isHeader, runNumber);
            });

            group.Cockpits.ForEach(cockpit =>
            {
                cockpit.AppendText(text, category, isHeader, runNumber);
            });
        }

        /// <summary>
        /// Writes diagnostic text to the programmable block's LCD screen
        /// </summary>
        /// <param name="text">Diagnostic text to display</param>
        void WriteToDiagnosticOutput(string text)
        {
            thisPb.AppendText(text);
        }

        string GetHeaderAnimation(int runNumber)
        {
            var title = "Farmhand";
            switch (runNumber)
            {
                case 0:
                    return $"{title} •––";
                case 1:
                    return $"{title} –•–";
                case 2:
                    return $"{title} ––•";
                default:
                    return title;
            }
        }
    }
}
