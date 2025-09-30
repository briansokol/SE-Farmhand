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
        bool isInitialized = false;
        readonly string Version = "v0.7.0";
        readonly string PublishedDate = "2025-09-29";

        // Coroutine management
        readonly List<IEnumerator<bool>> activeCoroutines = new List<IEnumerator<bool>>();
        int coroutineIndex = 0; // For round-robin execution

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
                // Print header once per cycle
                PrintHeader();

                // Print diagnostic info once per cycle
                farmGroups
                    .GetAllGroups()
                    .ForEach(farmGroup =>
                    {
                        WriteToDiagnosticOutput($"Group: {farmGroup.GroupName}");
                        if (farmGroup.FarmPlots.Count > 0)
                        {
                            WriteToDiagnosticOutput($"  Farm Plots: {farmGroup.FarmPlots.Count}");
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

                // Add Coroutines
                activeCoroutines.Add(FindBlocksCoroutine());
                activeCoroutines.Add(GetBlockStateCoroutine());
                activeCoroutines.Add(PrintOutputCoroutine());
            }

            // Execute coroutines
            ExecuteCoroutines();

            Echo($"Instructions: {Runtime.CurrentInstructionCount}/{Runtime.MaxInstructionCount}");
            Echo(
                $"Quota: {(float)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount:P2}"
            );
            Echo($"Active Coroutines: {activeCoroutines.Count}");
        }

        /// <summary>
        /// Executes active coroutines in round-robin fashion and manages their lifecycle
        /// </summary>
        void ExecuteCoroutines()
        {
            if (activeCoroutines.Count == 0)
                return;

            // Execute one coroutine per tick in round-robin fashion
            if (coroutineIndex >= activeCoroutines.Count)
                coroutineIndex = 0;

            var coroutine = activeCoroutines[coroutineIndex];
            try
            {
                if (!coroutine.MoveNext())
                {
                    // Coroutine completed, dispose and remove
                    coroutine.Dispose();
                    activeCoroutines.RemoveAt(coroutineIndex);

                    // If this was the first FindBlocks coroutine, mark as initialized
                    if (!isInitialized && activeCoroutines.Count == 0)
                    {
                        isInitialized = true;
                    }

                    // Adjust index after removal
                    if (coroutineIndex >= activeCoroutines.Count)
                        coroutineIndex = 0;
                }
                else
                {
                    // Move to next coroutine for next tick
                    coroutineIndex++;
                }
            }
            catch
            {
                // Error in coroutine, dispose and remove
                coroutine.Dispose();
                activeCoroutines.RemoveAt(coroutineIndex);

                // Adjust index after removal
                if (coroutineIndex >= activeCoroutines.Count)
                    coroutineIndex = 0;
            }
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

            yield return true; // Yield after block processing

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

            yield return true; // Yield after group name processing

            // Get all block group names on the grid to filter out invalid farm group names
            var gridBlockGroups = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(gridBlockGroups);

            // Remove those farm groups that are no longer referenced by any LCD panel or this programmable block
            farmGroups.RemoveGroupsNotInList(groupNames);

            yield return true; // Yield after removing old groups

            // For each group name, find and register the blocks
            foreach (var groupName in groupNames)
            {
                if (gridBlockGroups.Find(g => g.Name == groupName) == null)
                {
                    // Group is no longer accessible from this programmable block
                    continue;
                }

                var lcdPanelsInGroup = lcdPanels.FindAll(panel => panel.GroupName() == groupName);
                var cockpitsInGroup = cockpits.FindAll(cockpit => cockpit.GroupName() == groupName);
                farmGroups.ResetBlocks(groupName, lcdPanelsInGroup, cockpitsInGroup);
                yield return true;
                farmGroups.FindFarmPlots(groupName);
                yield return true;
                farmGroups.FindIrrigationSystems(groupName);
                yield return true;
                farmGroups.FindAirVents(groupName);
                yield return true;
                farmGroups.FindTimers(groupName);
                yield return true; // Yield after each group
            }
        }

        /// <summary>
        /// Updates state of all farm blocks and prepares output messages (coroutine version)
        /// </summary>
        IEnumerator<bool> GetBlockStateCoroutine()
        {
            Echo("GetBlockStateCoroutine");
            var allGroups = farmGroups.GetAllGroups();

            foreach (var farmGroup in allGroups)
            {
                var groupName = farmGroup.GroupName;

                int seedsNeeded = 0;
                int deadPlants = 0;
                Dictionary<string, int> plotSummary = new Dictionary<string, int>();
                Dictionary<string, int> yieldSummary = new Dictionary<string, int>();

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

                        if (farmPlot.IsPlantPlanted)
                        {
                            if (farmPlot.IsPlantAlive)
                            {
                                // Set the plot plant count
                                plotSummary[plantType] = plotSummary.ContainsKey(plantType)
                                    ? plotSummary[plantType] + 1
                                    : 1;

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
                                }
                            }
                            else
                            {
                                // Plant is dead
                                farmPlot.SetLightColor(thisPb.PlantedDeadColor);
                                deadPlants++;
                            }
                        }
                        else
                        {
                            // No plant
                            farmPlot.SetLightColor(thisPb.PlanterEmptyColor);
                            seedsNeeded += farmPlot.SeedsNeeded;
                        }

                        if (farmPlot.WaterFilledRatio > thisPb.WaterLowThreshold)
                        {
                            farmPlot.LightBlinkInterval = 0f;
                            farmPlot.LightBlinkLength = 1f;
                        }
                        else
                        {
                            farmPlot.LightBlinkInterval = 1f;
                            farmPlot.LightBlinkLength = 50f;
                            alertMessages.Add(
                                $"  {farmPlot.CustomName} Water Low: {farmPlot.WaterFilledRatio:P1}"
                            );
                            farmPlotsLowOnWater++;
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
                        alertMessages.Add($"  Dead Plants: {deadPlants}");
                    }
                    farmGroup.StateManager.UpdateState("OnCropDead", deadPlants > 0);

                    if (seedsNeeded > 0)
                    {
                        alertMessages.Add($"  Available Plots: {seedsNeeded}");
                    }
                    farmGroup.StateManager.UpdateState("OnCropAvailable", seedsNeeded > 0);

                    if (farmPlotsReadyToHarvest > 0)
                    {
                        alertMessages.Add($"  Harvest Ready Plots: {farmPlotsReadyToHarvest}");
                    }
                    farmGroup.StateManager.UpdateState("OnCropReady", farmPlotsReadyToHarvest > 0);

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
                            yieldMessages.Add(
                                $"  {entry.Key} ({entry.Value} Plot{(entry.Value == 1 ? "" : "s")}): {(plantYield == 0 ? "Growing" : plantYield.ToString())}"
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
            Echo("PrintOutputCoroutine");

            thisPb.FlushTextToScreen();
            yield return true; // Yield after programmable block screen flush

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
                yield return true; // Yield after each farm group's displays
            }
        }

        /// <summary>
        /// Prints the application header and version information
        /// </summary>
        void PrintHeader()
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

            farmGroups
                .GetAllGroups()
                .ForEach(farmGroup =>
                {
                    WriteToMainOutput(farmGroup.GroupName, header, "Header");
                    WriteToMainOutput(farmGroup.GroupName, "", "Header");
                });

            if (runNumber >= 3)
            {
                runNumber = 0;
            }
            else
            {
                runNumber += 1;
            }
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
