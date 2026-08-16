using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.Entities.Blocks;
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
        readonly string Version = "v1.3.0";
        readonly string PublishedDate = "2026-03-22";

        /// <summary>
        /// Increments once per completed pipeline cycle. Every per-block cache gates on this
        /// rather than on tick count, because a cycle can span many ticks on a large farm and
        /// configuration must stay stable for the whole of it.
        /// </summary>
        public int CycleNumber { get; private set; }

        /// <summary>Set to force a discovery rescan on the next cycle.</summary>
        public bool RescanRequested { get; set; }

        /// <summary>Cycles between periodic discovery rescans.</summary>
        public int RescanIntervalCycles = 30;

        int _lastDiscoveryCycle = int.MinValue;

        // Persistent EntityId to wrapper indexes for the tag-discovered display blocks. These
        // live on Program rather than on FarmGroup because the wrappers are constructed before
        // their group name is known: the group name is read from the wrapper's own custom data.
        readonly Dictionary<long, PlotLCD> _plotLcdsById = new Dictionary<long, PlotLCD>();
        readonly Dictionary<long, LcdPanel> _lcdPanelsById = new Dictionary<long, LcdPanel>();
        readonly Dictionary<long, TextSurfaceProvider> _textSurfaceProvidersById =
            new Dictionary<long, TextSurfaceProvider>();

        /// <summary>
        /// True when block discovery should run this cycle: first run, an explicit request,
        /// or the periodic interval has elapsed.
        /// </summary>
        bool IsDiscoveryDue()
        {
            if (RescanRequested)
                return true;
            if (_lastDiscoveryCycle == int.MinValue)
                return true;
            return CycleNumber - _lastDiscoveryCycle >= RescanIntervalCycles;
        }

        /// <summary>Records that discovery ran this cycle and clears any pending request.</summary>
        void MarkDiscoveryDone()
        {
            _lastDiscoveryCycle = CycleNumber;
            RescanRequested = false;
        }

        bool shiftSprites = false;

        public Program()
        {
            thisPb = new ProgrammableBlock(Me, this);
            farmGroups = new FarmGroups(GridTerminalSystem, this);
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        /// <summary>
        /// Required by Space Engineers API - currently not used
        /// </summary>
        public void Save() { }

        public void Main(string argument)
        {
            if (!string.IsNullOrWhiteSpace(argument))
            {
                DispatchCommand(argument.Trim());
            }

            RunOneTick();
            RenderEchoStatus();
        }

        void DispatchCommand(string argument)
        {
            if (argument.ToLower() == "cleanup")
            {
                CleanupAllCustomData();
            }
        }

        void RenderEchoStatus()
        {
            Echo("Farmhand");
            Echo($"{Version} | {PublishedDate}");
            Echo("");
            Echo($"Step: {_stepIndex} {_stepLabel}");
            Echo($"Cycle: {CycleNumber} ({TicksLastCycle} ticks)");
            Echo($"Instructions: {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}");
            Echo($"High water: {InstructionHighWater}");
        }

        /// <summary>
        /// Pushes per-cycle animation state onto persistent wrappers. Must run every cycle,
        /// not only when discovery runs, or blinking and multiplayer sprite refresh stall.
        /// </summary>
        void ApplyFrameState()
        {
            // Advance the animation frame. This lived inside PrintDiagnosticHeader, which was
            // only ever called from RestartCycle. Frame advancement now has exactly one owner.
            runNumber = runNumber >= 5 ? 0 : runNumber + 1;

            foreach (FarmGroup group in farmGroups.GetAllGroups())
            {
                group.RunNumber = runNumber;

                for (int i = 0; i < group.LcdPanels.Count; i++)
                {
                    group.LcdPanels[i].SetShiftSprites(shiftSprites);
                }
                for (int i = 0; i < group.TextSurfaceProviders.Count; i++)
                {
                    group.TextSurfaceProviders[i].SetShiftSprites(shiftSprites);
                }
            }

            for (int i = 0; i < plotLcds.Count; i++)
            {
                plotLcds[i].SetShiftSprites(shiftSprites);
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
        /// Prints header to LCD panels and cockpits
        /// </summary>
        void PrintHeaders()
        {
            foreach (var farmGroup in farmGroups.GetAllGroups())
                {
                    WriteToMainOutput(
                        farmGroup.GroupName,
                        "Farmhand",
                        "Header",
                        isHeader: true,
                        runNumber: runNumber
                    );
                    WriteToMainOutput(farmGroup.GroupName, "", "Header", isHeader: true);
                }
        }

        /// <summary>
        /// Discovers and categorizes blocks with [FarmLCD] tags for farm management
        /// </summary>
        void FindFarmLCDBlocks()
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

            // For each group name, find and register the blocks
            foreach (var groupName in groupNames)
            {
                var lcdPanelsInGroup = lcdPanels.FindAll(panel => panel.GroupName() == groupName);
                var surfaceProvidersInGroup = surfaceProviders.FindAll(provider =>
                    provider.GroupName() == groupName
                );
                farmGroups.ResetBlocks(groupName, lcdPanelsInGroup, surfaceProvidersInGroup);

                var group = farmGroups.GetGroup(groupName);
                group.ProgrammableBlock = thisPb;

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

            MarkDiscoveryDone();
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
                        var plotDetails = farmPlot.GetPlotDetails();

                        if (!farmPlot.IsPresent())
                        {
                            // A block we still hold a wrapper for has left the grid. Force
                            // discovery on the next cycle rather than waiting out the interval.
                            RescanRequested = true;
                        }

                        if (plotDetails != null)
                        {
                            stats.WaterUsagePerMinute += plotDetails.WaterUsage;
                        }

                        var lightInputs = new PlotLightInputs();
                        lightInputs.IsFunctional = farmPlot.IsFunctional();
                        lightInputs.IsPlanted = farmPlot.IsPlantPlanted;
                        lightInputs.IsAlive = farmPlot.IsPlantAlive;
                        lightInputs.IsFullyGrown = farmPlot.IsPlantFullyGrown;
                        lightInputs.HasDetails = plotDetails != null;
                        lightInputs.CropHealth = plotDetails != null ? plotDetails.CropHealth : 0f;
                        // FarmPlot.WaterFilledRatio is double (FarmPlot.cs:189); the cast is required.
                        lightInputs.WaterFilledRatio = (float)farmPlot.WaterFilledRatio;
                        lightInputs.HealthLowThreshold = healthLowThreshold;
                        lightInputs.WaterLowThreshold = waterLowThreshold;

                        var light = FarmPlot.DecidePlotLight(lightInputs);

                        if (controlLights)
                        {
                            switch (light.Role)
                            {
                                case PlotLightRole.Empty:
                                    farmPlot.SetLightColor(planterEmptyColor);
                                    break;
                                case PlotLightRole.Growing:
                                    farmPlot.SetLightColor(plantedAliveColor);
                                    break;
                                case PlotLightRole.Ready:
                                    farmPlot.SetLightColor(plantedReadyColor);
                                    break;
                                case PlotLightRole.Dead:
                                    farmPlot.SetLightColor(plantedDeadColor);
                                    break;
                                case PlotLightRole.WaterLow:
                                    farmPlot.SetLightColor(waterLowColor);
                                    break;
                            }
                            farmPlot.LightBlinkInterval = light.BlinkInterval;
                            farmPlot.LightBlinkLength = light.BlinkLength;
                        }

                        bool isHealthLow = lightInputs.IsPlanted
                            && lightInputs.IsAlive
                            && lightInputs.HasDetails
                            && lightInputs.CropHealth < healthLowThreshold;

                        if (lightInputs.IsPlanted)
                        {
                            if (lightInputs.IsAlive)
                            {
                                stats.TotalPlantedPlots++;

                                int currentCount;
                                if (stats.PlotSummary.TryGetValue(plantType, out currentCount))
                                {
                                    stats.PlotSummary[plantType] = currentCount + 1;
                                }
                                else
                                {
                                    stats.PlotSummary[plantType] = 1;
                                }

                                if (lightInputs.IsFullyGrown)
                                {
                                    stats.FarmPlotsReadyToHarvest++;

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
                                else if (isHealthLow)
                                {
                                    stats.AlertMessages.Add(
                                        $"Health Low: {plotDetails.CropHealth:P1} ({(string.IsNullOrEmpty(plantType) ? "" : plantType + ", ")}{farmPlot.CustomName})"
                                    );
                                    stats.DyingPlants++;
                                }
                                else if (plotDetails != null)
                                {
                                    float currentGrowth;
                                    if (
                                        !stats.GrowthSummary.TryGetValue(plantType, out currentGrowth)
                                        || (
                                            plotDetails.GrowthProgress > currentGrowth
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
                                stats.DeadPlants++;
                                // plotDetails can be null here; the original dereferenced it unguarded
                                // and the swallowed NullReferenceException skipped the whole step.
                                stats.CausesOfDeath.Add(
                                    plotDetails == null || string.IsNullOrWhiteSpace(plotDetails.CauseOfDeath)
                                        ? "Unknown"
                                        : plotDetails.CauseOfDeath
                                );
                            }
                        }
                        else
                        {
                            stats.SeedsNeeded += farmPlot.SeedsNeeded;
                        }

                        if (light.Role == PlotLightRole.WaterLow)
                        {
                            stats.AlertMessages.Add(
                                $"Water Low: {farmPlot.WaterFilledRatio:P1} ({(string.IsNullOrEmpty(plantType) ? "" : plantType + ", ")}{farmPlot.CustomName})"
                            );
                            stats.FarmPlotsLowOnWater++;
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
                    else
                    {
                        // No water tanks in group - ensure state is false to prevent spurious events
                        farmGroup.StateManager.UpdateState("OnWaterTankLow", false);
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
                        $"Water: {stats.WaterRatio:P1} ({stats.CurrentWaterL:F1} L / {stats.MaxWaterL:F1} L)"
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
                farmGroup.TextSurfaceProviders.ForEach(provider =>
                {
                    provider.SetFarmGroup(farmGroup);
                    provider.FlushTextToScreens();
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

            group.TextSurfaceProviders.ForEach(provider =>
            {
                provider.AppendText(text, category, isHeader, runNumber);
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
