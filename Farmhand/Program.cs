using System.Collections.Generic;
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

        readonly List<FarmPlot> farmPlots = new List<FarmPlot>();
        readonly List<IrrigationSystem> irrigationSystems = new List<IrrigationSystem>();
        readonly List<LcdPanel> lcdPanels = new List<LcdPanel>();
        readonly List<AirVent> airVents = new List<AirVent>();

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

                    farmPlot.LightIntensity = FarmPlot.DefaultLightIntensity;
                    farmPlot.LightRadius = FarmPlot.DefaultLightRadius;

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
                            }
                            else
                            {
                                // Plant is still growing
                                farmPlot.SetLightColor(plantedAliveColor);
                            }
                        }
                        else
                        {
                            // Plant is dead
                            farmPlot.SetLightColor(plantedDeadColor);
                            deadPlants++;
                        }
                    }
                    else
                    {
                        // No plant
                        farmPlot.SetLightColor(planterEmptyColor);
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
                        WriteToMainOutput(
                            $"{farmPlot.CustomName}: Water Low: {farmPlot.WaterFilledRatio:P0}",
                            "ShowErrors"
                        );
                    }
                });

                // Check air vents
                if (airVents.Count > 0)
                {
                    WriteToMainOutput("Atmosphere", "ShowAtmosphere");

                    var vent = airVents[0];

                    switch (vent.Status)
                    {
                        case VentStatus.Pressurizing:
                        case VentStatus.Pressurized:
                            WriteToMainOutput(
                                $"  Pressurized: {vent.OxygenLevel:P0}",
                                "ShowAtmosphere"
                            );
                            break;
                        case VentStatus.Depressurizing:
                        case VentStatus.Depressurized:
                            if (vent.CanPressurize)
                            {
                                WriteToMainOutput(
                                    $"  Depressurized (Room is Air Tight): {vent.OxygenLevel:P0}",
                                    "ShowAtmosphere"
                                );
                            }
                            else
                            {
                                WriteToMainOutput(
                                    $"  Depressurized: {vent.OxygenLevel:P0}",
                                    "ShowAtmosphere"
                                );
                            }
                            break;
                    }
                    WriteToMainOutput("", "ShowAtmosphere");
                }

                // Check irrigation systems
                if (irrigationSystems.Count > 0)
                {
                    WriteToMainOutput("Irrigation Systems", "ShowIrrigation");

                    float iceVolume = 0f;
                    float inventoryVolume = 0f;

                    irrigationSystems.ForEach(irrigationSystem =>
                    {
                        iceVolume += irrigationSystem.CurrentVolume;
                        inventoryVolume += irrigationSystem.MaxVolume;
                    });

                    WriteToMainOutput($"  Ice: {iceVolume / inventoryVolume:P0}", "ShowIrrigation");
                    WriteToMainOutput("", "ShowIrrigation");
                }

                // Final Summary
                if (seedsNeeded > 0 || deadPlants > 0 || yieldSummary.Count > 0)
                {
                    WriteToMainOutput("Yield", "ShowYield");
                    if (seedsNeeded > 0)
                    {
                        WriteToMainOutput($"  Available: {seedsNeeded}", "ShowYield");
                    }
                    if (deadPlants > 0)
                    {
                        WriteToMainOutput($"  Dead Plants: {deadPlants}", "ShowYield");
                    }
                    if (yieldSummary.Count > 0)
                    {
                        foreach (KeyValuePair<string, int> entry in yieldSummary)
                        {
                            WriteToMainOutput($"  {entry.Key} Ready: {entry.Value}", "ShowYield");
                        }
                    }
                    WriteToMainOutput("", "ShowYield");
                }
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
                    header = $"Farmhand - {runNumber}";
                    break;
                case 1:
                    header = $"Farmhand \\ {runNumber}";
                    break;
                case 2:
                    header = $"Farmhand | {runNumber}";
                    break;
                case 3:
                    header = $"Farmhand / {runNumber}";
                    break;
            }

            WriteToDiagnosticOutput(header);
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
