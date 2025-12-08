using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Shared helper methods for rendering and color calculations
    /// </summary>
    internal static class RenderHelpers
    {
        /// <summary>
        /// Cached rendering state for a farm plot to avoid redundant calculations
        /// </summary>
        internal struct PlotRenderState
        {
            public float GrowthProgress;
            public Color OutlineColor;
            public Color ProgressBarColor;
            public Color WaterBarColor;
        }

        /// <summary>
        /// Global sprite list cache (lazy initialization - populated on first use)
        /// </summary>
        private static List<string> _availableSprites = null;

        /// <summary>
        /// Resolves a plant sprite to use ColorfulIcons mod version if available
        /// </summary>
        /// <param name="plantId">Original plant ID (e.g., "MyObjectBuilder_Seed/Wheat")</param>
        /// <param name="surface">Text surface to retrieve sprite list (only used on first call)</param>
        /// <returns>ColorfulIcons sprite path if available, otherwise original plantId</returns>
        public static string ResolveColorfulIconSprite(string plantId, IMyTextSurface surface)
        {
            // Return early for invalid input
            if (string.IsNullOrEmpty(plantId))
            {
                return string.Empty;
            }

            // Initialize sprite cache on first use (only happens once)
            if (_availableSprites == null)
            {
                _availableSprites = new List<string>();
                if (surface != null)
                {
                    surface.GetSprites(_availableSprites);
                }
            }

            // Only transform if it's a MyObjectBuilder type
            if (!plantId.StartsWith("MyObjectBuilder_"))
            {
                return plantId;
            }

            // If no sprites available, return original
            if (_availableSprites.Count == 0)
            {
                return plantId;
            }

            // Transform to ColorfulIcons path
            var colorfulPath = plantId.Replace("MyObjectBuilder_", "ColorfulIcons_");

            // Check if ColorfulIcons version exists
            if (_availableSprites.Contains(colorfulPath))
            {
                return colorfulPath;
            }

            // Fallback to original
            return plantId;
        }

        /// <summary>
        /// Calculates all rendering colors for a farm plot in a single pass
        /// </summary>
        /// <param name="plot">The farm plot to evaluate</param>
        /// <param name="plotDetails">Pre-fetched plot details (or null if not available)</param>
        /// <param name="programmableBlock">Reference to programmable block for color configuration</param>
        /// <param name="isAlternateFrame">Whether this is an alternate frame for blinking effects</param>
        /// <returns>PlotRenderState containing all colors and growth progress</returns>
        public static PlotRenderState CalculatePlotRenderState(
            FarmPlot plot,
            FarmPlotDetails plotDetails,
            ProgrammableBlock programmableBlock,
            bool isAlternateFrame
        )
        {
            var state = new PlotRenderState();

            // Default colors if programmable block is not available
            if (programmableBlock == null)
            {
                state.OutlineColor = Color.Green;
                state.ProgressBarColor = Color.Green;
                state.WaterBarColor = Color.Blue;
                state.GrowthProgress = 0f;
                return state;
            }

            // Calculate growth progress
            if (plot.IsPlantPlanted && plot.IsPlantAlive && plotDetails != null)
            {
                state.GrowthProgress = plotDetails.GrowthProgress;
            }

            // Determine warning states
            bool isLowHealth =
                plot.IsPlantPlanted
                && plot.IsPlantAlive
                && plotDetails != null
                && plotDetails.CropHealth < programmableBlock.HealthLowThreshold;

            bool isLowWater =
                plot.IsFunctional()
                && plot.WaterFilledRatio <= programmableBlock.WaterLowThreshold
                && !isLowHealth;

            // Determine base plant status color
            Color baseColor;
            if (!plot.IsPlantPlanted)
            {
                baseColor = programmableBlock.PlanterEmptyColor;
            }
            else if (!plot.IsPlantAlive)
            {
                baseColor = programmableBlock.PlantedDeadColor;
            }
            else if (plot.IsPlantFullyGrown)
            {
                baseColor = programmableBlock.PlantedReadyColor;
            }
            else
            {
                baseColor = programmableBlock.PlantedAliveColor;
            }

            // Calculate outline color (blinks on low health or low water)
            if (isLowHealth && isAlternateFrame)
            {
                state.OutlineColor = programmableBlock.PlantedDeadColor;
            }
            else if (isLowWater && isAlternateFrame)
            {
                state.OutlineColor = programmableBlock.WaterLowColor;
            }
            else
            {
                state.OutlineColor = baseColor;
            }

            // Calculate progress bar color
            if (plot.IsPlantPlanted && plot.IsPlantAlive)
            {
                if (plot.IsPlantFullyGrown)
                {
                    // Ready plants always show ready color
                    state.ProgressBarColor = programmableBlock.PlantedReadyColor;
                }
                else if (isLowHealth)
                {
                    // Low health shows dead color (solid, no blink)
                    state.ProgressBarColor = programmableBlock.PlantedDeadColor;
                }
                else if (isLowWater && isAlternateFrame)
                {
                    // Low water blinks warning color
                    state.ProgressBarColor = programmableBlock.WaterLowColor;
                }
                else
                {
                    // Normal growing
                    state.ProgressBarColor = programmableBlock.PlantedAliveColor;
                }
            }
            else
            {
                state.ProgressBarColor = baseColor;
            }

            // Calculate water bar color (always solid, no blinking)
            if (plot.WaterFilledRatio <= programmableBlock.WaterLowThreshold)
            {
                state.WaterBarColor = programmableBlock.WaterLowColor;
            }
            else
            {
                state.WaterBarColor = baseColor;
            }

            return state;
        }

        /// <summary>
        /// Generates animated header text based on run number
        /// </summary>
        /// <param name="runNumber">Animation frame number (0-5)</param>
        /// <param name="title">The title text to display in the header (defaults to "Farmhand")</param>
        /// <param name="alignment">Text alignment (center includes start animation)</param>
        /// <returns>Formatted header string with animation</returns>
        public static string GetHeaderAnimation(
            int runNumber,
            string title = "Farmhand",
            TextAlignment alignment = TextAlignment.LEFT
        )
        {
            var animationStart = new[] { "––•", "–•–", "•––" };
            var animationEnd = new[] { "•––", "–•–", "––•" };
            var frameNumber = runNumber > 2 ? runNumber - 3 : runNumber;

            return alignment == TextAlignment.CENTER
                ? $"{animationStart[frameNumber % animationStart.Length]} {title} {animationEnd[frameNumber % animationEnd.Length]}"
                : $"{title} {animationEnd[frameNumber % animationEnd.Length]}";
        }
    }
}
