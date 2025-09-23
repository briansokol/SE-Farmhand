using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        readonly string Version = "v0.5";
        readonly string PublishedDate = "2025-09-22";

        readonly List<FarmPlot> farmPlots = new List<FarmPlot>();
        readonly List<IrrigationSystem> irrigationSystems = new List<IrrigationSystem>();
        readonly List<LcdPanel> lcdPanels = new List<LcdPanel>();
        readonly List<AirVent> airVents = new List<AirVent>();

        readonly StateManager stateManager = new StateManager();

        readonly ProgrammableBlock thisPb;
        int runNumber = 0;
        string groupName = "";

        public Program()
        {
            thisPb = new ProgrammableBlock(Me, this);
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save() { }

        public void Main()
        {
            if (runNumber == 0)
            {
                FindBlocks();
            }

            PrintHeader();

            WriteToDiagnosticOutput($"Farm Plots: {farmPlots.Count}");
            WriteToDiagnosticOutput($"Irrigation Systems: {irrigationSystems.Count}");
            WriteToDiagnosticOutput($"LCD Panels: {lcdPanels.Count}");
            WriteToDiagnosticOutput($"Air Vents: {airVents.Count}");
            WriteToDiagnosticOutput($"Timers: {stateManager.RegisteredTimerCount}");

            GetBlockState();
            PrintOutput();
        }

        void FindBlocks()
        {
            groupName = thisPb.GroupName;

            // Clear existing lists
            farmPlots.Clear();
            irrigationSystems.Clear();
            lcdPanels.Clear();
            airVents.Clear();
            stateManager.ClearTimers();

            // Find the block group by name
            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(groupName);

            // Find the farm plots in the group
            List<IMyFunctionalBlock> validFarmPlots = new List<IMyFunctionalBlock>();
            group?.GetBlocksOfType(validFarmPlots, block => FarmPlot.BlockIsValid(block));
            validFarmPlots.ForEach(block => farmPlots.Add(new FarmPlot(block, this)));

            // Find the irrigation systems in the group
            List<IMyGasGenerator> validIrrigationSystems = new List<IMyGasGenerator>();
            group?.GetBlocksOfType(
                validIrrigationSystems,
                block => IrrigationSystem.BlockIsValid(block)
            );
            validIrrigationSystems.ForEach(block =>
                irrigationSystems.Add(new IrrigationSystem(block, this))
            );

            // Find the LCD panels in the group
            List<IMyTextPanel> validLcdPanels = new List<IMyTextPanel>();
            group?.GetBlocksOfType(validLcdPanels, block => LcdPanel.BlockIsValid(block));
            validLcdPanels.ForEach(block => lcdPanels.Add(new LcdPanel(block, this)));

            // Find the air vents in the group
            List<IMyAirVent> validAirVents = new List<IMyAirVent>();
            group?.GetBlocksOfType(validAirVents, block => AirVent.BlockIsValid(block));
            validAirVents.ForEach(block => airVents.Add(new AirVent(block, this)));

            //Find the timers in the group
            List<IMyTimerBlock> validTimers = new List<IMyTimerBlock>();
            group?.GetBlocksOfType(validTimers, block => Timer.BlockIsValid(block));
            validTimers.ForEach(block => stateManager.RegisterTimer(new Timer(block, this)));
        }

        void GetBlockState()
        {
            int seedsNeeded = 0;
            int deadPlants = 0;
            Dictionary<string, int> plotSummary = new Dictionary<string, int>();
            Dictionary<string, int> yieldSummary = new Dictionary<string, int>();

            List<string> atmosphereMessages = new List<string>();
            List<string> irrigationMessages = new List<string>();
            List<string> yieldMessages = new List<string>();
            List<string> alertMessages = new List<string>();

            // Check farm plots
            if (farmPlots.Count > 0)
            {
                var farmPlotsLowOnWater = 0;
                var farmPlotsReadyToHarvest = 0;

                farmPlots.ForEach(farmPlot =>
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

                stateManager.UpdateState("OnWaterLow", farmPlotsLowOnWater > 0);

                // Check air vents
                if (airVents.Count > 0)
                {
                    var vent = airVents[0];

                    switch (vent.Status)
                    {
                        case VentStatus.Pressurizing:
                        case VentStatus.Pressurized:
                            atmosphereMessages.Add($"  Pressurized: {vent.OxygenLevel:P0}");
                            stateManager.UpdateState("OnPressurized", true);
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
                                atmosphereMessages.Add($"  Depressurized: {vent.OxygenLevel:P0}");
                            }
                            stateManager.UpdateState("OnPressurized", false);
                            break;
                    }
                }

                // Check irrigation systems
                if (irrigationSystems.Count > 0)
                {
                    float iceVolume = 0f;
                    float inventoryVolume = 0f;

                    irrigationSystems.ForEach(irrigationSystem =>
                    {
                        iceVolume += irrigationSystem.CurrentVolume;
                        inventoryVolume += irrigationSystem.MaxVolume;
                    });

                    var iceLowThreshold = thisPb.IceLowThreshold;
                    var iceRatio = inventoryVolume > 0 ? iceVolume / inventoryVolume : 0f;
                    irrigationMessages.Add($"  Ice: {iceRatio:P0}");

                    stateManager.UpdateState("OnIceLow", iceRatio < iceLowThreshold);
                }

                // Additional alerts
                if (deadPlants > 0)
                {
                    alertMessages.Add($"  Dead Plants: {deadPlants}");
                }
                stateManager.UpdateState("OnCropDead", deadPlants > 0);

                if (seedsNeeded > 0)
                {
                    alertMessages.Add($"  Available Plots: {seedsNeeded}");
                }
                stateManager.UpdateState("OnCropAvailable", seedsNeeded > 0);

                if (farmPlotsReadyToHarvest > 0)
                {
                    alertMessages.Add($"  Harvestable Plots: {farmPlotsReadyToHarvest}");
                }
                stateManager.UpdateState("OnCropReady", farmPlotsReadyToHarvest > 0);

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
                WriteToMainOutput("Alerts", "ShowErrors");
                alertMessages.ForEach(message => WriteToMainOutput(message, "ShowErrors"));
                WriteToMainOutput("", "ShowErrors");
            }

            if (atmosphereMessages.Count > 0)
            {
                WriteToMainOutput("Atmosphere", "ShowAtmosphere");
                atmosphereMessages.ForEach(message => WriteToMainOutput(message, "ShowAtmosphere"));
                WriteToMainOutput("", "ShowAtmosphere");
            }

            if (irrigationMessages.Count > 0)
            {
                WriteToMainOutput("Irrigation", "ShowIrrigation");
                irrigationMessages.ForEach(message => WriteToMainOutput(message, "ShowIrrigation"));
                WriteToMainOutput("", "ShowIrrigation");
            }

            if (yieldMessages.Count > 0)
            {
                WriteToMainOutput("Current Yield", "ShowYield");
                yieldMessages.ForEach(message => WriteToMainOutput(message, "ShowYield"));
                WriteToMainOutput("", "ShowYield");
            }
        }

        void PrintOutput()
        {
            thisPb.FlushTextToScreen();

            if (lcdPanels.Count > 0)
            {
                lcdPanels.ForEach(panel =>
                {
                    panel.FlushTextToScreen();
                });
            }
        }

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
            WriteToDiagnosticOutput($"Version: {Version} ({PublishedDate})");
            WriteToDiagnosticOutput($"Group: {groupName}");
            WriteToDiagnosticOutput("");

            WriteToMainOutput(header);
            WriteToMainOutput("");

            if (runNumber >= 3)
            {
                runNumber = 0;
            }
            else
            {
                runNumber += 1;
            }
        }

        void WriteToMainOutput(string text, string category = null)
        {
            if (lcdPanels.Count > 0)
            {
                lcdPanels.ForEach(panel =>
                {
                    panel.AppendText(text, category);
                });
            }
        }

        void WriteToDiagnosticOutput(string text)
        {
            thisPb.AppendText(text);
        }
    }
}
