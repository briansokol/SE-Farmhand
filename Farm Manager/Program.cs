using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        readonly Color planterEmptyColor = new Color(80, 0, 170);
        readonly Color plantedAliveColor = new Color(255, 255, 255);
        readonly Color plantedReadyColor = new Color(0, 255, 185);
        readonly Color plantedDeadColor = new Color(255, 0, 100);

        readonly StringBuilder mainOutput = new StringBuilder();
        readonly StringBuilder diagnosticOutput = new StringBuilder();

        readonly List<FarmPlot> farmPlots = new List<FarmPlot>();
        readonly List<IrrigationSystem> irrigationSystems = new List<IrrigationSystem>();
        readonly List<LcdPanel> lcdPanels = new List<LcdPanel>();
        readonly List<AirVent> airVents = new List<AirVent>();

        readonly ProgrammableBlock thisPb;
        int runNumber = 0;

        public Program()
        {
            thisPb = new ProgrammableBlock(Me, this);
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Save() { }

        public void Main(string argument, UpdateType updateSource)
        {
            // Main Logic Loop
            if ((updateSource & UpdateType.Update100) != 0)
            {
                diagnosticOutput.Clear();
                mainOutput.Clear();
                thisPb.ParseCustomData();
                PrintHeader();

                if (runNumber == 1)
                {
                    FindBlocks();
                }

                diagnosticOutput.AppendLine($"Farm Plots: {farmPlots.Count}");
                diagnosticOutput.AppendLine($"Irrigation Systems: {irrigationSystems.Count}");
                diagnosticOutput.AppendLine($"LCD Panels: {lcdPanels.Count}");
                diagnosticOutput.AppendLine($"Air Vents: {airVents.Count}");

                GetBlockState();
                PrintOutput();
            }
        }

        void FindBlocks()
        {
            var groupName = thisPb.GroupName;

            // Clear existing lists
            farmPlots.Clear();
            irrigationSystems.Clear();
            lcdPanels.Clear();
            airVents.Clear();

            // Find the block group by name
            IMyBlockGroup group = GridTerminalSystem.GetBlockGroupWithName(groupName);

            // Find the farm plots in the group
            List<IMyFunctionalBlock> validFarmPlots = new List<IMyFunctionalBlock>();
            group?.GetBlocksOfType(validFarmPlots, block => FarmPlot.BlockIsValid(block));
            validFarmPlots.ForEach(block =>
                farmPlots.Add(new FarmPlot(block, this, diagnosticOutput))
            );

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
        }

        void GetBlockState()
        {
            int seedsNeeded = 0;
            int deadPlants = 0;
            Dictionary<string, int> yieldSummary = new Dictionary<string, int>();

            // Check farm plots
            if (farmPlots.Count > 0)
            {
                farmPlots.ForEach(farmPlot =>
                {
                    var plantType = farmPlot.PlantType;
                    var plantYield = farmPlot.PlantYieldAmount;

                    farmPlot.LightIntensity = 2.0f;
                    farmPlot.LightRadius = 2.5f;

                    if (farmPlot.IsPlantPlanted)
                    {
                        // Set the yield summary
                        yieldSummary[plantType] = yieldSummary.ContainsKey(plantType)
                            ? yieldSummary[plantType] + plantYield
                            : plantYield;

                        if (farmPlot.IsPlantAlive)
                        {
                            if (farmPlot.IsPlantFullyGrown)
                            {
                                // Plant is ready to harvest
                                farmPlot.SetLightColor(plantedReadyColor);
                                farmPlot.LightBlinkInterval = 3f;
                                farmPlot.LightBlinkLength = 90f;
                            }
                            else
                            {
                                // Plant is still growing
                                farmPlot.SetLightColor(plantedAliveColor);
                                farmPlot.LightBlinkInterval = 0f;
                                farmPlot.LightBlinkLength = 1f;
                            }
                        }
                        else
                        {
                            // Plant is dead
                            farmPlot.SetLightColor(plantedDeadColor);
                            farmPlot.LightBlinkInterval = 0f;
                            farmPlot.LightBlinkLength = 1f;
                            deadPlants++;
                        }
                    }
                    else
                    {
                        // No plant
                        farmPlot.SetLightColor(planterEmptyColor);
                        farmPlot.LightBlinkInterval = 0f;
                        farmPlot.LightBlinkLength = 1f;
                        seedsNeeded += farmPlot.SeedsNeeded;
                    }

                    if (farmPlot.WaterFilledRatio > 0.2)
                    {
                        farmPlot.LightBlinkInterval = 0f;
                        farmPlot.LightBlinkLength = 1f;
                    }
                    else
                    {
                        farmPlot.LightBlinkInterval = 1f;
                        farmPlot.LightBlinkLength = 50f;
                        mainOutput.AppendLine(
                            $"{farmPlot.CustomName}: Water Low: {farmPlot.WaterFilledRatio:P0}"
                        );
                    }
                });

                // Check air vents
                if (airVents.Count > 0)
                {
                    mainOutput.AppendLine("Atmosphere");

                    var vent = airVents[0];

                    switch (vent.Status)
                    {
                        case VentStatus.Pressurizing:
                        case VentStatus.Pressurized:
                            mainOutput.AppendLine($"  Pressurized: {vent.OxygenLevel:P0}");
                            break;
                        case VentStatus.Depressurizing:
                        case VentStatus.Depressurized:
                            if (vent.CanPressurize)
                            {
                                mainOutput.AppendLine(
                                    $"  Depressurized (Room is Air Tight): {vent.OxygenLevel:P0}"
                                );
                            }
                            else
                            {
                                mainOutput.AppendLine($"  Depressurized: {vent.OxygenLevel:P0}");
                            }
                            break;
                    }
                    mainOutput.AppendLine("");
                }

                // Check irrigation systems
                if (irrigationSystems.Count > 0)
                {
                    mainOutput.AppendLine("Irrigation Systems");

                    float iceVolume = 0f;
                    float inventoryVolume = 0f;

                    irrigationSystems.ForEach(irrigationSystem =>
                    {
                        iceVolume += irrigationSystem.CurrentVolume;
                        inventoryVolume += irrigationSystem.MaxVolume;
                    });

                    mainOutput.AppendLine($"  Ice: {(iceVolume / inventoryVolume):P0}");
                    mainOutput.AppendLine("");
                }

                // Final Summary
                if (seedsNeeded > 0 || deadPlants > 0 || yieldSummary.Count > 0)
                {
                    mainOutput.AppendLine("Yield");
                    if (seedsNeeded > 0)
                    {
                        mainOutput.AppendLine($"  Available: {seedsNeeded}");
                    }
                    if (deadPlants > 0)
                    {
                        mainOutput.AppendLine($"  Dead Plants: {deadPlants}");
                    }
                    if (yieldSummary.Count > 0)
                    {
                        foreach (KeyValuePair<string, int> entry in yieldSummary)
                        {
                            mainOutput.AppendLine($"  {entry.Key} Ready: {entry.Value}");
                        }
                    }
                    mainOutput.AppendLine("");
                }
            }
        }

        void PrintOutput()
        {
            thisPb.WriteToLcd(diagnosticOutput.ToString());

            if (lcdPanels.Count > 0)
            {
                lcdPanels.ForEach(panel =>
                {
                    panel.WriteText(mainOutput.ToString());
                });
            }
        }

        void PrintHeader()
        {
            switch (runNumber)
            {
                case 0:
                case 4:
                    diagnosticOutput.AppendLine($"Farm Manager - {runNumber}");
                    mainOutput.AppendLine("Farm Manager -");
                    break;
                case 1:
                case 5:
                    diagnosticOutput.AppendLine($"Farm Manager \\ {runNumber}");
                    mainOutput.AppendLine("Farm Manager \\");
                    break;
                case 2:
                case 6:
                    diagnosticOutput.AppendLine($"Farm Manager | {runNumber}");
                    mainOutput.AppendLine("Farm Manager |");
                    break;
                case 3:
                case 7:
                    diagnosticOutput.AppendLine($"Farm Manager / {runNumber}");
                    mainOutput.AppendLine("Farm Manager /");
                    break;
            }

            diagnosticOutput.AppendLine($"Group: {thisPb.GroupName}");
            diagnosticOutput.AppendLine("");
            mainOutput.AppendLine("");

            if (runNumber >= 7)
            {
                runNumber = 0;
            }
            else
            {
                runNumber += 1;
            }
        }
    }
}
