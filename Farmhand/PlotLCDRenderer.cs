using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Provides sprite-based rendering for PlotLCD displays (512x73 resolution)
    /// Supports split display for showing two farm plots side-by-side
    /// </summary>
    internal class PlotLCDRenderer
    {
        // Layout constants for 512x73 display
        private const int PADDING = 3;
        private const int WATER_BAR_WIDTH = 30;
        private const int WATER_BAR_WIDTH_HALF = 15; // Half width for split display
        private const int ICON_SIZE = 67; // Fills vertical space (73 - 6px padding = 67)
        private const int BAR_PADDING = 2; // Padding around progress bars
        private const int CENTER_PADDING = 0; // Padding between left and right displays

        private readonly IMyTextSurface _surface;
        private readonly FarmPlot _leftFarmPlot;
        private readonly FarmPlot _rightFarmPlot;
        private readonly int _runNumber;
        private readonly ProgrammableBlock _programmableBlock;
        private readonly RectangleF _viewport;
        private readonly Vector2 _textureSize;

        // Used to force redraw of sprites on server clients
        private readonly bool _shiftSprites;

        /// <summary>
        /// Initializes a new PlotLCDRenderer for the specified surface
        /// </summary>
        /// <param name="surface">The text surface to draw on (512x73 LCD panel)</param>
        /// <param name="leftFarmPlot">The farm plot to display on the left side (null if not found)</param>
        /// <param name="rightFarmPlot">The farm plot to display on the right side (null if not found)</param>
        /// <param name="runNumber">Animation frame number for blinking effects (0-5)</param>
        /// <param name="programmableBlock">Reference to programmable block for color configuration</param>
        /// <param name="shiftSprites">Whether to shift sprites for redraw on server clients</param>
        public PlotLCDRenderer(
            IMyTextSurface surface,
            FarmPlot leftFarmPlot,
            FarmPlot rightFarmPlot,
            int runNumber,
            ProgrammableBlock programmableBlock,
            bool shiftSprites = false
        )
        {
            _surface = surface;
            _leftFarmPlot = leftFarmPlot;
            _rightFarmPlot = rightFarmPlot;
            _runNumber = runNumber;
            _programmableBlock = programmableBlock;
            _shiftSprites = shiftSprites;
            _textureSize = _surface.TextureSize;
            _viewport = new RectangleF(
                (_textureSize - _surface.SurfaceSize) / 2f,
                _surface.SurfaceSize
            );
        }

        /// <summary>
        /// Draws the plot status display (or "Farm Plot Not Found" message)
        /// Supports split display when multiple plots are present
        /// </summary>
        public void DrawPlotStatus()
        {
            using (var frame = _surface.DrawFrame())
            {
                // Shift sprite array every other render to force redraw on server clients
                if (_shiftSprites)
                {
                    frame.Add(new MySprite());
                }

                // Determine display mode based on which plots exist
                bool hasLeftPlot = _leftFarmPlot != null;
                bool hasRightPlot = _rightFarmPlot != null;

                if (!hasLeftPlot && !hasRightPlot)
                {
                    DrawNotFoundMessage(frame);
                }
                else if (hasLeftPlot && hasRightPlot)
                {
                    // Draw both plots in split display
                    DrawHalfPlotDisplay(frame, _leftFarmPlot, true);
                    DrawHalfPlotDisplay(frame, _rightFarmPlot, false);
                }
                else if (hasLeftPlot)
                {
                    // Draw only left plot in full display
                    DrawFullPlotDisplay(frame, _leftFarmPlot);
                }
                else
                {
                    // Draw only right plot in full display
                    DrawFullPlotDisplay(frame, _rightFarmPlot);
                }
            }
        }

        /// <summary>
        /// Draws "Farm Plot Not Found" message centered on screen
        /// </summary>
        private void DrawNotFoundMessage(MySpriteDrawFrame frame)
        {
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = "Farm Plot Not Found",
                    Position = CreatePosition(_viewport.Width / 2f, _viewport.Height / 3),
                    RotationOrScale = 0.8f,
                    Color = _surface.ScriptForegroundColor,
                    Alignment = TextAlignment.CENTER,
                    FontId = "White",
                }
            );
        }

        /// <summary>
        /// Draws a half-width plot display for split display mode
        /// </summary>
        /// <param name="frame">The sprite draw frame</param>
        /// <param name="farmPlot">The farm plot to display</param>
        /// <param name="isLeftSide">True for left side, false for right side</param>
        private void DrawHalfPlotDisplay(
            MySpriteDrawFrame frame,
            FarmPlot farmPlot,
            bool isLeftSide
        )
        {
            // Calculate half-width bounds
            // Total width: 512px, center padding: 2px
            // Left side: 0-255px (255px width)
            // Right side: 257-512px (255px width)
            float halfWidth = (_viewport.Width - CENTER_PADDING) / 2f;
            float startX = isLeftSide ? 0f : (halfWidth + CENTER_PADDING);

            // Calculate usable area (after padding)
            float usableWidth = halfWidth - (2 * PADDING);
            float usableHeight = _viewport.Height - (2 * PADDING);

            // Calculate positions (all elements vertically centered)
            float centerY = _viewport.Height / 2f;

            // Icon on the left of the half-display
            float iconX = startX + PADDING + (ICON_SIZE / 2f);
            float iconY = centerY;

            // Water bar next to icon (half width)
            float waterBarX = startX + PADDING + ICON_SIZE + (WATER_BAR_WIDTH_HALF / 2f);
            float waterBarY = centerY;

            // Growth bar fills remaining space in this half
            float remainingWidth = usableWidth - ICON_SIZE - WATER_BAR_WIDTH_HALF;
            float growthBarWidth = remainingWidth;
            float growthBarX =
                startX + PADDING + ICON_SIZE + WATER_BAR_WIDTH_HALF + (growthBarWidth / 2f);
            float growthBarY = centerY;

            // Determine if this is an alternate frame for blinking (odd frames: 1, 3, 5)
            bool isAlternateFrame = (_runNumber % 2) == 1;

            // Get colors for the plot
            Color outlineColor = GetPlotColor(farmPlot, isAlternateFrame);
            Color progressBarColor = GetProgressBarColor(farmPlot, isAlternateFrame);

            // Get growth progress
            float growthProgress = 0f;
            if (farmPlot.IsPlantPlanted && farmPlot.IsPlantAlive)
            {
                var details = farmPlot.GetPlotDetails();
                if (details != null)
                {
                    growthProgress = details.GrowthProgress;
                }
            }

            // Draw plant icon or circle for empty plots
            if (farmPlot.IsPlantPlanted)
            {
                DrawPlantIcon(frame, iconX, iconY, farmPlot);
            }
            else
            {
                DrawEmptyPlotIcon(frame, iconX, iconY);
            }

            // Draw water bar (vertical)
            DrawWaterBar(
                frame,
                waterBarX,
                waterBarY,
                usableHeight,
                outlineColor,
                farmPlot,
                WATER_BAR_WIDTH_HALF
            );

            // Draw growth bar (horizontal)
            DrawGrowthBar(
                frame,
                growthBarX,
                growthBarY,
                growthBarWidth,
                usableHeight,
                growthProgress,
                outlineColor,
                progressBarColor
            );
        }

        /// <summary>
        /// Draws a full-width plot display for single plot mode
        /// </summary>
        /// <param name="frame">The sprite draw frame</param>
        /// <param name="farmPlot">The farm plot to display</param>
        private void DrawFullPlotDisplay(MySpriteDrawFrame frame, FarmPlot farmPlot)
        {
            // Calculate usable area (after padding)
            float usableWidth = _viewport.Width - (2 * PADDING);
            float usableHeight = _viewport.Height - (2 * PADDING);

            // Calculate positions (all elements vertically centered)
            float centerY = _viewport.Height / 2f;

            // Icon on the left
            float iconX = PADDING + (ICON_SIZE / 2f);
            float iconY = centerY;

            // Water bar next to icon (full width)
            float waterBarX = PADDING + ICON_SIZE + (WATER_BAR_WIDTH / 2f);
            float waterBarY = centerY;

            // Growth bar fills remaining space
            float remainingWidth = usableWidth - ICON_SIZE - WATER_BAR_WIDTH;
            float growthBarWidth = remainingWidth;
            float growthBarX = PADDING + ICON_SIZE + WATER_BAR_WIDTH + (growthBarWidth / 2f);
            float growthBarY = centerY;

            // Determine if this is an alternate frame for blinking (odd frames: 1, 3, 5)
            bool isAlternateFrame = (_runNumber % 2) == 1;

            // Get colors for the plot
            Color outlineColor = GetPlotColor(farmPlot, isAlternateFrame);
            Color progressBarColor = GetProgressBarColor(farmPlot, isAlternateFrame);

            // Get growth progress
            float growthProgress = 0f;
            if (farmPlot.IsPlantPlanted && farmPlot.IsPlantAlive)
            {
                var details = farmPlot.GetPlotDetails();
                if (details != null)
                {
                    growthProgress = details.GrowthProgress;
                }
            }

            // Draw plant icon or circle for empty plots
            if (farmPlot.IsPlantPlanted)
            {
                DrawPlantIcon(frame, iconX, iconY, farmPlot);
            }
            else
            {
                DrawEmptyPlotIcon(frame, iconX, iconY);
            }

            // Draw water bar (vertical) with full width
            DrawWaterBar(
                frame,
                waterBarX,
                waterBarY,
                usableHeight,
                outlineColor,
                farmPlot,
                WATER_BAR_WIDTH
            );

            // Draw growth bar (horizontal)
            DrawGrowthBar(
                frame,
                growthBarX,
                growthBarY,
                growthBarWidth,
                usableHeight,
                growthProgress,
                outlineColor,
                progressBarColor
            );
        }

        /// <summary>
        /// Draws the plant icon texture
        /// </summary>
        private void DrawPlantIcon(MySpriteDrawFrame frame, float x, float y, FarmPlot farmPlot)
        {
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = farmPlot.PlantId,
                    Position = CreatePosition(x, y),
                    Size = new Vector2(ICON_SIZE, ICON_SIZE),
                    Alignment = TextAlignment.CENTER,
                }
            );
        }

        /// <summary>
        /// Draws a CircleHollow icon for empty plots
        /// </summary>
        private void DrawEmptyPlotIcon(MySpriteDrawFrame frame, float x, float y)
        {
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "Circle",
                    Position = CreatePosition(x, y),
                    Size = new Vector2(ICON_SIZE * 1 / 3, ICON_SIZE * 1 / 3),
                    Color = _programmableBlock.PlanterEmptyColor,
                    Alignment = TextAlignment.CENTER,
                }
            );
        }

        /// <summary>
        /// Draws a vertical water bar with layered background
        /// </summary>
        private void DrawWaterBar(
            MySpriteDrawFrame frame,
            float x,
            float y,
            float barHeight,
            Color outlineColor,
            FarmPlot farmPlot,
            int waterBarWidth
        )
        {
            // Layer 1: Colored background (outline)
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = CreatePosition(x, y),
                    Size = new Vector2(waterBarWidth, barHeight),
                    Color = outlineColor,
                    Alignment = TextAlignment.CENTER,
                }
            );

            // Layer 2: Black background (1px smaller on all sides)
            float blackWidth = waterBarWidth - (2 * BAR_PADDING);
            float blackHeight = barHeight - (2 * BAR_PADDING);
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = CreatePosition(x, y),
                    Size = new Vector2(blackWidth, blackHeight),
                    Color = _surface.ScriptBackgroundColor,
                    Alignment = TextAlignment.CENTER,
                }
            );

            // Layer 3: Water fill (grows from bottom, 1px padding from black background)
            float waterRatio = (float)farmPlot.WaterFilledRatio;
            if (waterRatio > 0f)
            {
                // Determine water bar fill color
                Color waterBarColor = GetWaterBarColor(farmPlot);

                float fillWidth = blackWidth - (2 * BAR_PADDING);
                float fillMaxHeight = blackHeight - (2 * BAR_PADDING);
                float fillHeight = fillMaxHeight * waterRatio;

                // Position from bottom (negative y offset for bottom alignment)
                float fillY = y + (fillMaxHeight / 2f) - (fillHeight / 2f);

                frame.Add(
                    new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Position = CreatePosition(x, fillY),
                        Size = new Vector2(fillWidth, fillHeight),
                        Color = waterBarColor,
                        Alignment = TextAlignment.CENTER,
                    }
                );
            }
        }

        /// <summary>
        /// Draws a horizontal growth bar with layered background
        /// </summary>
        private void DrawGrowthBar(
            MySpriteDrawFrame frame,
            float x,
            float y,
            float barWidth,
            float barHeight,
            float growthProgress,
            Color outlineColor,
            Color progressBarColor
        )
        {
            // Layer 1: Colored background (outline)
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = CreatePosition(x, y),
                    Size = new Vector2(barWidth, barHeight),
                    Color = outlineColor,
                    Alignment = TextAlignment.CENTER,
                }
            );

            // Layer 2: Black background (1px smaller on all sides)
            float blackWidth = barWidth - (2 * BAR_PADDING);
            float blackHeight = barHeight - (2 * BAR_PADDING);
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = CreatePosition(x, y),
                    Size = new Vector2(blackWidth, blackHeight),
                    Color = _surface.ScriptBackgroundColor,
                    Alignment = TextAlignment.CENTER,
                }
            );

            // Layer 3: Growth fill (grows from left, 1px padding from black background)
            if (growthProgress > 0f)
            {
                float fillMaxWidth = blackWidth - (2 * BAR_PADDING);
                float fillHeight = blackHeight - (2 * BAR_PADDING);
                float fillWidth = fillMaxWidth * growthProgress;

                // Position from left (negative x offset for left alignment)
                float fillX = x - (fillMaxWidth / 2f) + (fillWidth / 2f);

                frame.Add(
                    new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Position = CreatePosition(fillX, y),
                        Size = new Vector2(fillWidth, fillHeight),
                        Color = progressBarColor,
                        Alignment = TextAlignment.CENTER,
                    }
                );
            }
        }

        /// <summary>
        /// Creates a position vector adjusted for the viewport
        /// </summary>
        private Vector2 CreatePosition(float x, float y)
        {
            return new Vector2(x, y) + _viewport.Position;
        }

        /// <summary>
        /// Determines the appropriate color for the water bar fill
        /// </summary>
        private Color GetWaterBarColor(FarmPlot farmPlot)
        {
            if (_programmableBlock == null)
            {
                return Color.Blue;
            }

            // Water bar shows water warning or base plant status
            if (farmPlot.WaterFilledRatio <= _programmableBlock.WaterLowThreshold)
            {
                return _programmableBlock.WaterLowColor;
            }
            else
            {
                return GetPlotColor(farmPlot, false); // Base plant color (no blinking)
            }
        }

        /// <summary>
        /// Determines the appropriate color for a farm plot based on its plant state
        /// (Same logic as SpriteRenderer.GetPlotColor)
        /// </summary>
        private Color GetPlotColor(FarmPlot plot, bool isAlternateFrame)
        {
            if (_programmableBlock == null)
            {
                return Color.Green;
            }

            // Get plot details to check health
            var plotDetails = plot.GetPlotDetails();

            // Check if plot has low health (takes priority over low water)
            // Outline blinks when health is low
            bool isLowHealth =
                plot.IsPlantPlanted
                && plot.IsPlantAlive
                && plotDetails != null
                && plotDetails.CropHealth < _programmableBlock.HealthLowThreshold;

            if (isLowHealth && isAlternateFrame)
            {
                return _programmableBlock.PlantedDeadColor;
            }

            // Check if plot has low water and should show warning color on alternate frames
            bool isLowWater =
                plot.IsFunctional()
                && plot.WaterFilledRatio <= _programmableBlock.WaterLowThreshold
                && !isLowHealth; // Only show water warning if health is not low

            if (isLowWater && isAlternateFrame)
            {
                return _programmableBlock.WaterLowColor;
            }

            if (plot.IsPlantPlanted)
            {
                if (plot.IsPlantAlive)
                {
                    if (plot.IsPlantFullyGrown)
                    {
                        return _programmableBlock.PlantedReadyColor;
                    }
                    else
                    {
                        return _programmableBlock.PlantedAliveColor;
                    }
                }
                else
                {
                    return _programmableBlock.PlantedDeadColor;
                }
            }
            else
            {
                return _programmableBlock.PlanterEmptyColor;
            }
        }

        /// <summary>
        /// Determines the appropriate color for a farm plot's progress bar based on its plant state
        /// (Same logic as SpriteRenderer.GetProgressBarColor)
        /// </summary>
        private Color GetProgressBarColor(FarmPlot plot, bool isAlternateFrame)
        {
            if (_programmableBlock == null)
            {
                return Color.Green;
            }

            // Get plot details to check health
            var plotDetails = plot.GetPlotDetails();

            // Check if plot has low health
            bool isLowHealth =
                plot.IsPlantPlanted
                && plot.IsPlantAlive
                && plotDetails != null
                && plotDetails.CropHealth < _programmableBlock.HealthLowThreshold;

            // Check if plot has low water (only matters if health is OK)
            bool isLowWater =
                plot.IsFunctional()
                && plot.WaterFilledRatio <= _programmableBlock.WaterLowThreshold
                && !isLowHealth; // Only show water warning if health is not low

            if (plot.IsPlantPlanted)
            {
                if (plot.IsPlantAlive)
                {
                    // Priority: Ready color > Low health warning > Growing color
                    if (plot.IsPlantFullyGrown)
                    {
                        // Plant is ready - show ready color even if health is low
                        return _programmableBlock.PlantedReadyColor;
                    }
                    else if (isLowHealth)
                    {
                        // Plant is growing but health is low - show dead color (solid)
                        return _programmableBlock.PlantedDeadColor;
                    }
                    else if (isLowWater && isAlternateFrame)
                    {
                        // Plant is growing, health OK, but water low - blink water warning
                        return _programmableBlock.WaterLowColor;
                    }
                    else
                    {
                        // Plant is growing normally
                        return _programmableBlock.PlantedAliveColor;
                    }
                }
                else
                {
                    return _programmableBlock.PlantedDeadColor;
                }
            }
            else
            {
                return _programmableBlock.PlanterEmptyColor;
            }
        }
    }
}
