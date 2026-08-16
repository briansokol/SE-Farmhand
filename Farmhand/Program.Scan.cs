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
        /// <summary>Chunk size for the plot scan; a budget check also runs every iteration.</summary>
        const int ScanChunkSize = 20;

        /// <summary>
        /// Accumulates per-plot statistics into each group's scratch buffer, yielding on
        /// chunk boundaries and whenever the instruction budget trips.
        /// </summary>
        IEnumerator<YieldReason> StepScanPlots()
        {
            int counter = 0;

            for (int g = 0; g < _groupSnapshot.Count; g++)
            {
                FarmGroup farmGroup = _groupSnapshot[g];
                FarmStats stats = farmGroup.ScratchStats;
                stats.Clear();

                if (farmGroup.FarmPlots.Count == 0)
                {
                    continue;
                }

                // Cache threshold values and colors for performance (avoid repeated property access)
                var healthLowThreshold = thisPb.HealthLowThreshold;
                var waterLowThreshold = thisPb.WaterLowThreshold;
                var planterEmptyColor = thisPb.PlanterEmptyColor;
                var plantedAliveColor = thisPb.PlantedAliveColor;
                var plantedReadyColor = thisPb.PlantedReadyColor;
                var plantedDeadColor = thisPb.PlantedDeadColor;
                var waterLowColor = thisPb.WaterLowColor;
                var controlLights = thisPb.ControlFarmPlotLights;

                for (int i = 0; i < farmGroup.FarmPlots.Count; i++)
                {
                    AccumulatePlot(
                        farmGroup.FarmPlots[i], stats,
                        healthLowThreshold, waterLowThreshold,
                        planterEmptyColor, plantedAliveColor, plantedReadyColor,
                        plantedDeadColor, waterLowColor, controlLights);

                    counter++;
                    if (counter % ScanChunkSize == 0) yield return YieldReason.ChunkBoundary;
                    if (BudgetExceeded()) yield return YieldReason.BudgetHit;
                }
            }
        }

        /// <summary>
        /// Accumulates the statistics for a single farm plot and, when light control is
        /// enabled, pushes the decided light state onto the plot.
        /// </summary>
        void AccumulatePlot(
            FarmPlot farmPlot,
            FarmStats stats,
            float healthLowThreshold,
            float waterLowThreshold,
            Color planterEmptyColor,
            Color plantedAliveColor,
            Color plantedReadyColor,
            Color plantedDeadColor,
            Color waterLowColor,
            bool controlLights
        )
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
        }

        /// <summary>
        /// Accumulates air vent, irrigation, water tank, and algae farm statistics into each
        /// group's scratch buffer.
        /// </summary>
        IEnumerator<YieldReason> StepScanSupport()
        {
            int counter = 0;

            for (int g = 0; g < _groupSnapshot.Count; g++)
            {
                FarmGroup farmGroup = _groupSnapshot[g];
                // The original wrapped all of this in `if (farmGroup.FarmPlots.Count > 0)`,
                // so a group with no farm plots accumulated nothing.
                if (farmGroup.FarmPlots.Count == 0) continue;

                FarmStats stats = farmGroup.ScratchStats;

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

                    stats.IceRatio = inventoryVolume > 0 ? iceVolume / inventoryVolume : 0f;
                    stats.CurrentIceKg = iceVolume / 0.37f;
                    stats.MaxIceKg = inventoryVolume / 0.37f;
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

                    stats.WaterRatio =
                        maxWaterVolume > 0 ? (float)(waterVolume / maxWaterVolume) : 0f;
                    stats.CurrentWaterL = (float)waterVolume;
                    stats.MaxWaterL = (float)maxWaterVolume;
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

                counter++;
                if (counter % ScanChunkSize == 0) yield return YieldReason.ChunkBoundary;
                if (BudgetExceeded()) yield return YieldReason.BudgetHit;
            }
        }

        /// <summary>
        /// Placeholder; Task 10 publishes each group's scratch buffer into Stats and
        /// evaluates the StateManager events that the scan steps no longer raise.
        /// </summary>
        IEnumerator<YieldReason> StepCommitSnapshot()
        {
            yield return YieldReason.ChunkBoundary;
        }
    }
}
