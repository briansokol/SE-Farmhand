using System.Collections;
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
        readonly string Version = "v0.7.0";
        readonly string PublishedDate = "2025-09-29";

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
            string header = "";
            switch (runNumber)
            {
                case 0:
                    header = "Farmhand -";
                    break;
                case 1:
                    header = "Farmhand \\";
                    break;
                case 2:
                    header = "Farmhand |";
                    break;
                case 3:
                    header = "Farmhand /";
                    break;
            }

            WriteToDiagnosticOutput(header);
            WriteToDiagnosticOutput($"{Version} | ({PublishedDate})");
            WriteToDiagnosticOutput("");

            if (runNumber >= 3)
            {
                runNumber = 0;
            }
            else
            {
                runNumber += 1;
            }

            // Print diagnostic info once per cycle
            farmGroups
                .GetAllGroups()
                .ForEach(farmGroup =>
                {
                    WriteToDiagnosticOutput($"Group: {farmGroup.GroupName}");
                    if (farmGroup.FarmPlots.Count > 0)
                    {
                        WriteToDiagnosticOutput($"  Farm Plots: {farmGroup.FarmPlots.Count}");
                        // farmGroup.FarmPlots.ForEach(farmPlot =>
                        // {
                        //     WriteToDiagnosticOutput(
                        //         $"{farmPlot.GetDetailedInfoWithoutRequiredInput()}"
                        //     );
                        // });
                    }
                    if (farmGroup.IrrigationSystems.Count > 0)
                    {
                        WriteToDiagnosticOutput(
                            $"  Irrigation Systems: {farmGroup.IrrigationSystems.Count}"
                        );
                    }
                    if (farmGroup.LcdPanels.Count > 0)
                    {
                        WriteToDiagnosticOutput($"  LCD Panels: {farmGroup.LcdPanels.Count}");
                    }
                    if (farmGroup.Cockpits.Count > 0)
                    {
                        WriteToDiagnosticOutput($"  Control Seats: {farmGroup.Cockpits.Count}");
                    }
                    if (farmGroup.AirVents.Count > 0)
                    {
                        WriteToDiagnosticOutput($"  Air Vents: {farmGroup.AirVents.Count}");
                    }
                    if (farmGroup.StateManager.RegisteredTimerCount > 0)
                    {
                        WriteToDiagnosticOutput(
                            $"  Timers: {farmGroup.StateManager.RegisteredTimerCount}"
                        );
                    }
                });
        }

        /// <summary>
        /// Prints header to LCD panels and cockpits (coroutine version)
        /// </summary>
        IEnumerator<bool> PrintHeaderCoroutine()
        {
            string header = "";
            switch (runNumber - 1)
            {
                case -1:
                    header = "Farmhand /";
                    break;
                case 0:
                    header = "Farmhand -";
                    break;
                case 1:
                    header = "Farmhand \\";
                    break;
                case 2:
                    header = "Farmhand |";
                    break;
            }

            farmGroups
                .GetAllGroups()
                .ForEach(farmGroup =>
                {
                    WriteToMainOutput(farmGroup.GroupName, header, "Header");
                    WriteToMainOutput(farmGroup.GroupName, "", "Header");
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

                int seedsNeeded = 0;
                int deadPlants = 0;
                float waterUsagePerMinute = 0f;
                List<string> causesOfDeath = new List<string>();
                Dictionary<string, int> plotSummary = new Dictionary<string, int>();
                Dictionary<string, int> yieldSummary = new Dictionary<string, int>();
                Dictionary<string, float> growthSummary = new Dictionary<string, float>();

                List<string> farmPlotMessages = new List<string>();
                List<string> atmosphereMessages = new List<string>();
                List<string> irrigationMessages = new List<string>();
                List<string> yieldMessages = new List<string>();
                List<string> alertMessages = new List<string>();

                // Check farm plots
                if (farmGroup.FarmPlots.Count > 0)
                {
                    var farmPlotsLowOnWater = 0;
                    var farmPlotsReadyToHarvest = 0;

                    farmGroup.FarmPlots.ForEach(farmPlot =>
                    {
                        var plantType = farmPlot.PlantType;
                        var plantYield = farmPlot.PlantYieldAmount;
                        var plotDetails = farmPlot.GetPlotDetails();

                        waterUsagePerMinute += plotDetails.WaterUsage;

                        if (farmPlot.IsPlantPlanted)
                        {
                            if (farmPlot.IsPlantAlive)
                            {
                                // Set the plot plant count
                                plotSummary[plantType] = plotSummary.ContainsKey(plantType)
                                    ? plotSummary[plantType] + 1
                                    : 1;

                                if (plotDetails.CropHealth < 1f)
                                {
                                    alertMessages.Add(
                                        $"  Health Low: {plotDetails.CropHealth:P1} ({farmPlot.PlantType}, {farmPlot.CustomName})"
                                    );
                                }

                                if (farmPlot.IsPlantFullyGrown)
                                {
                                    // Plant is ready to harvest
                                    farmPlot.SetLightColor(thisPb.PlantedReadyColor);
                                    farmPlotsReadyToHarvest++;

                                    // Set the yield summary
                                    yieldSummary[plantType] = yieldSummary.ContainsKey(plantType)
                                        ? yieldSummary[plantType] + plantYield
                                        : plantYield;
                                }
                                else
                                {
                                    // Plant is still growing
                                    farmPlot.SetLightColor(thisPb.PlantedAliveColor);

                                    if (
                                        !growthSummary.ContainsKey(plantType)
                                        || (
                                            plotDetails.GrowthProgress > growthSummary[plantType]
                                            && plotDetails.GrowthProgress < 1f
                                        )
                                    )
                                    {
                                        growthSummary[plantType] = plotDetails.GrowthProgress;
                                    }
                                }
                            }
                            else
                            {
                                // Plant is dead
                                farmPlot.SetLightColor(thisPb.PlantedDeadColor);
                                deadPlants++;
                                causesOfDeath.Add(
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
                            seedsNeeded += farmPlot.SeedsNeeded;
                        }

                        if (
                            farmPlot.IsFunctional()
                            && farmPlot.WaterFilledRatio <= thisPb.WaterLowThreshold
                        )
                        {
                            farmPlot.LightBlinkInterval = 1f;
                            farmPlot.LightBlinkLength = 50f;
                            alertMessages.Add(
                                $"  Water Low: {farmPlot.WaterFilledRatio:P1} ({farmPlot.PlantType}, {farmPlot.CustomName})"
                            );
                            farmPlotsLowOnWater++;
                        }
                        else
                        {
                            farmPlot.LightBlinkInterval = 0f;
                            farmPlot.LightBlinkLength = 1f;
                        }
                    });

                    farmGroup.StateManager.UpdateState("OnWaterLow", farmPlotsLowOnWater > 0);

                    // Check air vents
                    if (farmGroup.AirVents.Count > 0)
                    {
                        var vent = farmGroup.AirVents[0];

                        switch (vent.Status)
                        {
                            case VentStatus.Pressurizing:
                            case VentStatus.Pressurized:
                                atmosphereMessages.Add($"  Pressurized: {vent.OxygenLevel:P0}");
                                farmGroup.StateManager.UpdateState("OnPressurized", true);
                                break;
                            case VentStatus.Depressurizing:
                            case VentStatus.Depressurized:
                                if (vent.CanPressurize)
                                {
                                    atmosphereMessages.Add(
                                        $"  Depressurized (Room is Air Tight): {vent.OxygenLevel:P0}"
                                    );
                                }
                                else
                                {
                                    atmosphereMessages.Add(
                                        $"  Depressurized: {vent.OxygenLevel:P0}"
                                    );
                                }
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
                        var iceRatio = inventoryVolume > 0 ? iceVolume / inventoryVolume : 0f;
                        irrigationMessages.Add($"  Ice: {iceRatio:P0}");

                        farmGroup.StateManager.UpdateState("OnIceLow", iceRatio < iceLowThreshold);
                    }

                    // Additional alerts
                    if (deadPlants > 0)
                    {
                        farmPlotMessages.Add(
                            $"  Dead Plants: {deadPlants} ({string.Join(", ", causesOfDeath.Distinct())})"
                        );
                    }
                    farmGroup.StateManager.UpdateState("OnCropDead", deadPlants > 0);

                    if (seedsNeeded > 0)
                    {
                        farmPlotMessages.Add($"  Available Plots: {seedsNeeded}");
                    }
                    farmGroup.StateManager.UpdateState("OnCropAvailable", seedsNeeded > 0);

                    if (farmPlotsReadyToHarvest > 0)
                    {
                        farmPlotMessages.Add($"  Harvest Ready Plots: {farmPlotsReadyToHarvest}");
                    }
                    farmGroup.StateManager.UpdateState("OnCropReady", farmPlotsReadyToHarvest > 0);

                    if (waterUsagePerMinute > 0f)
                    {
                        farmPlotMessages.Add($"  Water Usage: {waterUsagePerMinute:F1} L/min");
                    }

                    // Check if all planted crops are ready to harvest
                    var totalPlantedPlots = 0;
                    farmGroup.FarmPlots.ForEach(farmPlot =>
                    {
                        if (farmPlot.IsPlantPlanted && farmPlot.IsPlantAlive)
                        {
                            totalPlantedPlots++;
                        }
                    });

                    farmGroup.StateManager.UpdateState(
                        "OnAllCropsReady",
                        totalPlantedPlots > 0 && farmPlotsReadyToHarvest == totalPlantedPlots
                    );

                    // Yield summary
                    if (plotSummary.Count > 0)
                    {
                        foreach (KeyValuePair<string, int> entry in plotSummary)
                        {
                            int plantYield = yieldSummary.ContainsKey(entry.Key)
                                ? yieldSummary[entry.Key]
                                : 0;
                            float growthProgress = growthSummary.ContainsKey(entry.Key)
                                ? growthSummary[entry.Key]
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
                                $"  {entry.Key} ({entry.Value} Plot{(entry.Value == 1 ? "" : "s")}): {string.Join(", ", yieldText)}"
                            );
                        }
                    }
                }

                if (alertMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Alerts", "ShowAlerts");
                    alertMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowAlerts")
                    );
                    WriteToMainOutput(groupName, "", "ShowAlerts");
                }

                if (farmPlotMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Farm Plots", "ShowFarmPlots");
                    farmPlotMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowFarmPlots")
                    );
                    WriteToMainOutput(groupName, "", "ShowFarmPlots");
                }

                if (atmosphereMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Atmosphere", "ShowAtmosphere");
                    atmosphereMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowAtmosphere")
                    );
                    WriteToMainOutput(groupName, "", "ShowAtmosphere");
                }

                if (irrigationMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Irrigation", "ShowIrrigation");
                    irrigationMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowIrrigation")
                    );
                    WriteToMainOutput(groupName, "", "ShowIrrigation");
                }

                if (yieldMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Current Yield", "ShowYield");
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
                    panel.FlushTextToScreen();
                });
                farmGroup.Cockpits.ForEach(cockpit =>
                {
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
        void WriteToMainOutput(string groupName, string text, string category = null)
        {
            var group = farmGroups.GetGroup(groupName);

            group.LcdPanels.ForEach(panel =>
            {
                panel.AppendText(text, category);
            });

            group.Cockpits.ForEach(cockpit =>
            {
                cockpit.AppendText(text, category);
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
    }
}
