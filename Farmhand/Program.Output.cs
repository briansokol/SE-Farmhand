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
        /// <summary>Builds output message lines for every group and writes them to text displays.</summary>
        IEnumerator<YieldReason> StepBuildTextOutput()
        {
            // The programmable block's own buffer is emptied by every FlushTextToScreen, so
            // the diagnostic header has to be rebuilt each cycle before StepRenderText flushes
            // it. The old per-cycle queue rebuild used to own this call.
            PrintDiagnosticHeader();
            yield return YieldReason.ChunkBoundary;

            for (int g = 0; g < _groupSnapshot.Count; g++)
            {
                FarmGroup farmGroup = _groupSnapshot[g];
                PrintHeadersForGroup(farmGroup);
                BuildMessagesForGroup(farmGroup);
                yield return YieldReason.ChunkBoundary;
                if (BudgetExceeded()) yield return YieldReason.BudgetHit;
            }
        }

        /// <summary>
        /// Prints header to the LCD panels and cockpits of a single farm group
        /// </summary>
        void PrintHeadersForGroup(FarmGroup farmGroup)
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

        /// <summary>
        /// Builds the categorised text message lines for a single farm group from its
        /// published statistics and appends them to the group's text displays.
        /// </summary>
        void BuildMessagesForGroup(FarmGroup farmGroup)
        {
            var groupName = farmGroup.GroupName;
            var stats = farmGroup.Stats;

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

        /// <summary>Flushes buffered text to each display, one display per chunk.</summary>
        IEnumerator<YieldReason> StepRenderText()
        {
            // The programmable block's own screen, flushed first exactly as the
            // original RenderTextDisplays did.
            thisPb.FlushTextToScreen();
            yield return YieldReason.ChunkBoundary;

            for (int g = 0; g < _groupSnapshot.Count; g++)
            {
                FarmGroup farmGroup = _groupSnapshot[g];
                for (int i = 0; i < farmGroup.LcdPanels.Count; i++)
                {
                    LcdPanel panel = farmGroup.LcdPanels[i];
                    // Graphical panels are skipped here: LcdPanel.FlushTextToScreen calls
                    // DrawGraphicalUI internally, which would draw them twice per cycle
                    // alongside StepFlushGraphical.
                    if (!panel.IsGraphicalMode())
                    {
                        // SetFarmGroup must precede the flush; the panel renders from it.
                        panel.SetFarmGroup(farmGroup);
                        panel.FlushTextToScreen();
                    }
                    if (BudgetExceeded()) yield return YieldReason.BudgetHit;
                }
                for (int i = 0; i < farmGroup.TextSurfaceProviders.Count; i++)
                {
                    farmGroup.TextSurfaceProviders[i].SetFarmGroup(farmGroup);
                    farmGroup.TextSurfaceProviders[i].FlushTextToScreens();
                    if (BudgetExceeded()) yield return YieldReason.BudgetHit;
                }
                yield return YieldReason.ChunkBoundary;
            }
        }

        /// <summary>Placeholder; Task 11 replaces this with chunked sprite accumulation.</summary>
        IEnumerator<YieldReason> StepBuildGraphicalSprites()
        {
            yield return YieldReason.ChunkBoundary;
        }

        /// <summary>Draws graphical displays. Task 11 splits the build and flush phases.</summary>
        IEnumerator<YieldReason> StepFlushGraphical()
        {
            for (int g = 0; g < _groupSnapshot.Count; g++)
            {
                FarmGroup farmGroup = _groupSnapshot[g];
                for (int i = 0; i < farmGroup.LcdPanels.Count; i++)
                {
                    if (farmGroup.LcdPanels[i].IsGraphicalMode())
                    {
                        // SetFarmGroup must precede the draw, exactly as the original
                        // RenderGraphicalDisplays did.
                        farmGroup.LcdPanels[i].SetFarmGroup(farmGroup);
                        farmGroup.LcdPanels[i].DrawGraphicalUI();
                    }
                    yield return YieldReason.ChunkBoundary;
                    if (BudgetExceeded()) yield return YieldReason.BudgetHit;
                }
            }
        }

        /// <summary>Draws each PlotLCD, one per chunk.</summary>
        IEnumerator<YieldReason> StepRenderPlotLCDs()
        {
            for (int i = 0; i < plotLcds.Count; i++)
            {
                plotLcds[i].Render(runNumber, thisPb);
                yield return YieldReason.ChunkBoundary;
                if (BudgetExceeded()) yield return YieldReason.BudgetHit;
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
    }
}
