using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

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
        readonly string plotLcdTag = "PlotLCD";
        readonly List<PlotLCD> plotLcds = new List<PlotLCD>();
        int runNumber = 0;
        readonly string Version = "v1.1.0";
        readonly string PublishedDate = "2025-12-01";

        // Step-based state machine management
        delegate void Step();
        readonly List<Step> stepQueue = new List<Step>();
        int currentStepIndex = 0;
        double currentCycleTime = 0;
        double lastCycleTime = 0;

        bool shiftSprites = false;

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
        public void Main(string argument)
        {
            // Handle cleanup command
            if (!string.IsNullOrWhiteSpace(argument) && argument.Trim().ToLower() == "cleanup")
            {
                CleanupAllCustomData();
                return;
            }

            // Build step queue when starting a new cycle
            if (currentStepIndex >= stepQueue.Count)
            {
                RestartCycle();
            }

            // Execute current step
            ExecuteCurrentStep();

            // Accumulate cycle time
            currentCycleTime += Runtime.TimeSinceLastRun.TotalMilliseconds;

            Echo($"Instructions: {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}");
            Echo(
                $"Quota: {(float)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount:P2}"
            );
            Echo($"Current Step: {currentStepIndex}/{stepQueue.Count}");
            Echo($"Last Cycle Time: {lastCycleTime / 1000:F2}s");
        }

        /// <summary>
        /// Executes the current step in the step queue and advances to the next step
        /// </summary>
        void ExecuteCurrentStep()
        {
            if (currentStepIndex >= stepQueue.Count)
                return;

            try
            {
                // Execute the current step
                stepQueue[currentStepIndex]();

                // Advance to next step
                currentStepIndex++;
            }
            catch
            {
                // Error in step, skip to next
                currentStepIndex++;
            }
        }

        /// <summary>
        /// Restarts the cycle by building a new step queue and printing diagnostic information
        /// </summary>
        void RestartCycle()
        {
            // Save the completed cycle time and reset for new cycle
            lastCycleTime = currentCycleTime;
            currentCycleTime = 0;
            currentStepIndex = 0;

            // Print diagnostic header
            PrintDiagnosticHeader();

            // Build the step queue dynamically based on what blocks exist
            BuildStepQueue();
        }

        /// <summary>
        /// Builds the step queue dynamically based on what blocks are currently available
        /// </summary>
        void BuildStepQueue()
        {
            stepQueue.Clear();

            // Always find FarmLCD blocks first
            stepQueue.Add(FindFarmLCDBlocks);

            // Always find PlotLCD blocks
            stepQueue.Add(FindPlotLCDBlocks);

            // Only print headers if we have FarmLCD displays
            bool hasFarmLcdDisplays = farmGroups
                .GetAllGroups()
                .Any(g => g.LcdPanels.Count > 0 || g.Cockpits.Count > 0);

            if (hasFarmLcdDisplays)
            {
                stepQueue.Add(PrintHeaders);
            }

            // Only update block state if we have farm groups
            if (farmGroups.GetAllGroups().Any())
            {
                stepQueue.Add(UpdateBlockState);
            }

            // Render text displays if we have any text-mode LCDs or cockpits
            bool hasTextDisplays = farmGroups
                .GetAllGroups()
                .Any(g => g.LcdPanels.Any(p => !p.IsGraphicalMode()) || g.Cockpits.Count > 0);

            if (hasTextDisplays)
            {
                stepQueue.Add(RenderTextDisplays);
            }

            // Render graphical displays if we have any graphical-mode LCDs
            bool hasGraphicalDisplays = farmGroups
                .GetAllGroups()
                .Any(g => g.LcdPanels.Any(p => p.IsGraphicalMode()));

            if (hasGraphicalDisplays)
            {
                stepQueue.Add(RenderGraphicalDisplays);
            }

            // Render PlotLCDs if we have any
            if (plotLcds.Count > 0)
            {
                stepQueue.Add(RenderPlotLCDs);
            }

            shiftSprites = !shiftSprites;
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

            if (runNumber >= 5)
            {
                runNumber = 0;
            }
            else
            {
                runNumber += 1;
            }

            WriteToDiagnosticOutput(header, true);
            WriteToDiagnosticOutput($"{Version} | ({PublishedDate})", true);
            WriteToDiagnosticOutput("", true);

            // Print diagnostic info once per cycle
            farmGroups
                .GetAllGroups()
                .ForEach(farmGroup =>
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
                });

            // Print PlotLCD count (independent of groups)
            if (plotLcds.Count > 0)
            {
                WriteToDiagnosticOutput($"Plot LCDs: {plotLcds.Count}");
            }
        }

        /// <summary>
        /// Prints header to LCD panels and cockpits
        /// </summary>
        void PrintHeaders()
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
        }

        /// <summary>
        /// Discovers and categorizes blocks with [FarmLCD] tags for farm management
        /// </summary>
        void FindFarmLCDBlocks()
        {
            var lcdPanels = new List<LcdPanel>();
            var cockpits = new List<Cockpit>();

            // Find the blocks with [FarmLCD] in their custom name
            List<IMyTerminalBlock> lcdTaggedBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName($"[{lcdTag}]", lcdTaggedBlocks);

            lcdTaggedBlocks.ForEach(block =>
            {
                if (Cockpit.BlockIsValid(block))
                {
                    cockpits.Add(new Cockpit(block as IMyCockpit, this, shiftSprites));
                }
                else if (LcdPanel.BlockIsValid(block as IMyFunctionalBlock))
                {
                    lcdPanels.Add(new LcdPanel(block as IMyTextPanel, this, shiftSprites));
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

            // For each group name, find and register the blocks
            foreach (var groupName in groupNames)
            {
                var lcdPanelsInGroup = lcdPanels.FindAll(panel => panel.GroupName() == groupName);
                var cockpitsInGroup = cockpits.FindAll(cockpit => cockpit.GroupName() == groupName);
                farmGroups.ResetBlocks(groupName, lcdPanelsInGroup, cockpitsInGroup);

                var group = farmGroups.GetGroup(groupName);
                group.ProgrammableBlock = thisPb;
                group.RunNumber = runNumber;

                farmGroups.FindFarmPlots(groupName);
                farmGroups.FindIrrigationSystems(groupName);
                farmGroups.FindWaterTanks(groupName);
                farmGroups.FindAirVents(groupName);
                farmGroups.FindSolarFoodGenerators(groupName);
                farmGroups.FindTimers(groupName);
                farmGroups.FindActionRelays(groupName);
                farmGroups.FindBroadcastControllers(groupName);
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
                    var plotLcd = new PlotLCD(block as IMyTextPanel, this, shiftSprites);
                    plotLcds.Add(plotLcd);

                    // Find nearby farm plot (only if resolution is correct)
                    if (plotLcd.IsCorrectResolution)
                    {
                        plotLcd.FindNearbyFarmPlot();
                    }
                }
            });
        }

        /// <summary>
        /// Updates state of all farm blocks and prepares output messages
        /// </summary>
        void UpdateBlockState()
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
                    // Cache threshold values and colors for performance (avoid repeated property access)
                    var healthLowThreshold = thisPb.HealthLowThreshold;
                    var waterLowThreshold = thisPb.WaterLowThreshold;
                    var planterEmptyColor = thisPb.PlanterEmptyColor;
                    var plantedAliveColor = thisPb.PlantedAliveColor;
                    var plantedReadyColor = thisPb.PlantedReadyColor;
                    var plantedDeadColor = thisPb.PlantedDeadColor;
                    var waterLowColor = thisPb.WaterLowColor;
                    var controlLights = thisPb.ControlFarmPlotLights;

                    farmGroup.FarmPlots.ForEach(farmPlot =>
                    {
                        var plantType = farmPlot.PlantType;
                        var plantYield = farmPlot.PlantYieldAmount;

                        // Cache plot details to avoid repeated string parsing
                        var plotDetails = farmPlot.GetPlotDetails();

                        if (plotDetails != null)
                        {
                            stats.WaterUsagePerMinute += plotDetails.WaterUsage;
                        }

                        if (farmPlot.IsPlantPlanted)
                        {
                            if (farmPlot.IsPlantAlive)
                            {
                                // Set the plot plant count using TryGetValue for better performance
                                int currentCount;
                                if (stats.PlotSummary.TryGetValue(plantType, out currentCount))
                                {
                                    stats.PlotSummary[plantType] = currentCount + 1;
                                }
                                else
                                {
                                    stats.PlotSummary[plantType] = 1;
                                }

                                // Priority: Ready to harvest > Low health warning > Growing
                                if (farmPlot.IsPlantFullyGrown)
                                {
                                    // Plant is ready to harvest - show ready color even if health is low
                                    if (controlLights)
                                    {
                                        farmPlot.SetLightColor(plantedReadyColor);
                                    }
                                    stats.FarmPlotsReadyToHarvest++;

                                    // Set the yield summary using TryGetValue for better performance
                                    int currentYield;
                                    if (stats.YieldSummary.TryGetValue(plantType, out currentYield))
                                    {
                                        stats.YieldSummary[plantType] = currentYield + plantYield;
                                    }
                                    else
                                    {
                                        stats.YieldSummary[plantType] = plantYield;
                                    }
                                }
                                else if (
                                    plotDetails != null
                                    && plotDetails.CropHealth < healthLowThreshold
                                )
                                {
                                    // Plant is growing but health is critically low - set dead color and blink
                                    if (controlLights)
                                    {
                                        farmPlot.SetLightColor(plantedDeadColor);
                                        farmPlot.LightBlinkInterval = 2f;
                                        farmPlot.LightBlinkLength = 50f;
                                    }
                                    stats.AlertMessages.Add(
                                        $"Health Low: {plotDetails.CropHealth:P1} ({(string.IsNullOrEmpty(farmPlot.PlantType) ? "" : farmPlot.PlantType + ", ")}{farmPlot.CustomName})"
                                    );
                                    stats.DyingPlants++;
                                }
                                else
                                {
                                    // Plant is still growing normally
                                    if (controlLights)
                                    {
                                        farmPlot.SetLightColor(plantedAliveColor);
                                    }

                                    if (plotDetails != null)
                                    {
                                        // Use TryGetValue for better performance
                                        float currentGrowth;
                                        if (
                                            !stats.GrowthSummary.TryGetValue(
                                                plantType,
                                                out currentGrowth
                                            )
                                            || (
                                                plotDetails.GrowthProgress > currentGrowth
                                                && plotDetails.GrowthProgress < 1f
                                            )
                                        )
                                        {
                                            stats.GrowthSummary[plantType] =
                                                plotDetails.GrowthProgress;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                // Plant is dead
                                if (controlLights)
                                {
                                    farmPlot.SetLightColor(plantedDeadColor);
                                }
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
                            if (controlLights)
                            {
                                farmPlot.SetLightColor(planterEmptyColor);
                            }
                            stats.SeedsNeeded += farmPlot.SeedsNeeded;
                        }

                        // Check for low water (only if plant is not ready and health is OK)
                        bool isHealthLow =
                            farmPlot.IsPlantPlanted
                            && farmPlot.IsPlantAlive
                            && plotDetails != null
                            && plotDetails.CropHealth < healthLowThreshold;

                        bool isReady = farmPlot.IsPlantPlanted && farmPlot.IsPlantFullyGrown;

                        if (
                            farmPlot.IsFunctional()
                            && farmPlot.WaterFilledRatio <= waterLowThreshold
                            && !isHealthLow
                            && !isReady
                        )
                        {
                            if (controlLights)
                            {
                                farmPlot.SetLightColor(waterLowColor);
                                farmPlot.LightBlinkInterval = 2f;
                                farmPlot.LightBlinkLength = 50f;
                            }
                            stats.AlertMessages.Add(
                                $"Water Low: {farmPlot.WaterFilledRatio:P1} ({(string.IsNullOrEmpty(farmPlot.PlantType) ? "" : farmPlot.PlantType + ", ")}{farmPlot.CustomName})"
                            );
                            stats.FarmPlotsLowOnWater++;
                        }
                        else if (!isHealthLow && !isReady)
                        {
                            // Only turn off blinking if health is OK and plant is not ready
                            if (controlLights)
                            {
                                farmPlot.LightBlinkInterval = 0f;
                                farmPlot.LightBlinkLength = 1f;
                            }
                        }
                        else if (isReady)
                        {
                            // Plant is ready - ensure blinking is off
                            if (controlLights)
                            {
                                farmPlot.LightBlinkInterval = 0f;
                                farmPlot.LightBlinkLength = 1f;
                            }
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

                    // Check water tanks
                    if (farmGroup.WaterTanks.Count > 0)
                    {
                        double waterVolume = 0.0;
                        double maxWaterVolume = 0.0;

                        farmGroup.WaterTanks.ForEach(waterTank =>
                        {
                            waterVolume += waterTank.CurrentVolume;
                            maxWaterVolume += waterTank.MaxVolume;
                        });

                        var waterTankLowThreshold = thisPb.WaterTankLowThreshold;
                        stats.WaterRatio =
                            maxWaterVolume > 0 ? (float)(waterVolume / maxWaterVolume) : 0f;
                        stats.CurrentWaterL = (float)waterVolume;
                        stats.MaxWaterL = (float)maxWaterVolume;

                        farmGroup.StateManager.UpdateState(
                            "OnWaterTankLow",
                            stats.WaterRatio < waterTankLowThreshold
                        );
                    }

                    // Check solar food generators
                    if (farmGroup.SolarFoodGenerators.Count > 0)
                    {
                        float totalItemsPerMinute = 0f;
                        float minTimeRemaining = float.MaxValue;

                        farmGroup.SolarFoodGenerators.ForEach(generator =>
                        {
                            totalItemsPerMinute += generator.ItemsPerMinute;
                            if (generator.TimeRemainingUntilNextBatch < minTimeRemaining)
                            {
                                minTimeRemaining = generator.TimeRemainingUntilNextBatch;
                            }
                        });

                        stats.TotalFoodItemsPerMinute = totalItemsPerMinute;
                        stats.MinTimeRemainingUntilNextBatch =
                            minTimeRemaining != float.MaxValue ? minTimeRemaining : 0f;
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
                var solarFoodGeneratorMessages = new List<string>();
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
                else
                {
                    stats.AlertMessages.Add("No Working Irrigation Systems!");
                }

                var waterTankMessages = new List<string>();

                if (farmGroup.WaterTanks.Count > 0)
                {
                    waterTankMessages.Add(
                        $"Water: {stats.WaterRatio:P0} ({stats.CurrentWaterL:F1} L / {stats.MaxWaterL:F1} L)"
                    );
                }

                if (farmGroup.SolarFoodGenerators.Count > 0)
                {
                    solarFoodGeneratorMessages.Add(
                        $"Production Rate: {stats.TotalFoodItemsPerMinute:F2} items/min"
                    );

                    // Format time with appropriate units
                    string timeText;
                    float timeInSeconds = stats.MinTimeRemainingUntilNextBatch;
                    if (timeInSeconds < 60f)
                    {
                        timeText = $"{timeInSeconds:F1} sec";
                    }
                    else if (timeInSeconds < 3600f)
                    {
                        timeText = $"{timeInSeconds / 60f:F1} min";
                    }
                    else
                    {
                        timeText = $"{timeInSeconds / 3600f:F1} hr";
                    }

                    solarFoodGeneratorMessages.Add($"Next Production: {timeText}");
                }

                // Yield summary
                if (stats.PlotSummary.Count > 0)
                {
                    foreach (KeyValuePair<string, int> entry in stats.PlotSummary)
                    {
                        // Use TryGetValue for better performance
                        int plantYield;
                        if (!stats.YieldSummary.TryGetValue(entry.Key, out plantYield))
                        {
                            plantYield = 0;
                        }

                        float growthProgress;
                        if (!stats.GrowthSummary.TryGetValue(entry.Key, out growthProgress))
                        {
                            growthProgress = 0f;
                        }

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

                if (waterTankMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Water Tanks", "ShowWaterTanks", isHeader: true);
                    waterTankMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowWaterTanks")
                    );
                    WriteToMainOutput(groupName, "", "ShowWaterTanks");
                }

                if (solarFoodGeneratorMessages.Count > 0)
                {
                    WriteToMainOutput(
                        groupName,
                        "Solar Food Generators",
                        "ShowSolarFoodGenerators",
                        isHeader: true
                    );
                    solarFoodGeneratorMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowSolarFoodGenerators")
                    );
                    WriteToMainOutput(groupName, "", "ShowSolarFoodGenerators");
                }

                if (yieldMessages.Count > 0)
                {
                    WriteToMainOutput(groupName, "Current Yield", "ShowYield", isHeader: true);
                    yieldMessages.ForEach(message =>
                        WriteToMainOutput(groupName, message, "ShowYield")
                    );
                    WriteToMainOutput(groupName, "", "ShowYield");
                }
            }
        }

        /// <summary>
        /// Renders text-mode LCD panels and cockpit screens
        /// </summary>
        void RenderTextDisplays()
        {
            thisPb.FlushTextToScreen();

            var allGroups = farmGroups.GetAllGroups();
            foreach (var farmGroup in allGroups)
            {
                // Process text-mode LCD panels
                foreach (var panel in farmGroup.LcdPanels)
                {
                    if (!panel.IsGraphicalMode())
                    {
                        panel.SetFarmGroup(farmGroup);
                        panel.FlushTextToScreen();
                    }
                }

                // Process cockpits (always text mode)
                farmGroup.Cockpits.ForEach(cockpit =>
                {
                    cockpit.SetFarmGroup(farmGroup);
                    cockpit.FlushTextToScreens();
                });
            }
        }

        /// <summary>
        /// Renders graphical-mode LCD panels using sprites
        /// </summary>
        void RenderGraphicalDisplays()
        {
            var allGroups = farmGroups.GetAllGroups();
            foreach (var farmGroup in allGroups)
            {
                // Process graphical-mode LCD panels
                foreach (var panel in farmGroup.LcdPanels)
                {
                    if (panel.IsGraphicalMode())
                    {
                        panel.SetFarmGroup(farmGroup);
                        panel.DrawGraphicalUI();
                    }
                }
            }
        }

        /// <summary>
        /// Renders PlotLCD displays
        /// </summary>
        void RenderPlotLCDs()
        {
            foreach (var plotLcd in plotLcds)
            {
                plotLcd.Render(runNumber, thisPb);
            }
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
        void WriteToDiagnosticOutput(string text, bool header = false)
        {
            thisPb.AppendText(text, header);
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

                // Clean cockpits
                foreach (var block in farmGroup.Cockpits)
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
