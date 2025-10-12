using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Represents a layout row that can contain 1-2 farm plot groups
    /// </summary>
    internal class LayoutRow
    {
        public IGrouping<string, FarmPlot> LeftGroup { get; set; }
        public IGrouping<string, FarmPlot> RightGroup { get; set; }
    }

    /// <summary>
    /// Provides sprite-based graphical rendering for text surfaces
    /// </summary>
    internal class SpriteRenderer
    {
        // Layout constants
        private const float SPACING = 10f;
        private const float PLOT_PADDING = 2f;
        private const float WATER_INNER_PADDING = 2f;
        private const float ICON_SCALE = 0.8f;
        private const float HEADER_SCALE = 0.8f;
        private const float HEADER_Y_POSITION = 10f;
        private const float HEADER_FONT_HEIGHT = 24f;

        // Instance fields
        private readonly IMyTextSurface _surface;
        private readonly FarmGroup _farmGroup;
        private readonly Vector2 _viewport;

        // Derived layout values
        private readonly float _iconSize;
        private readonly float _iconPadding;
        private readonly float _leftMargin;
        private readonly float _waterRectHeight;
        private readonly float _rectHeight;
        private readonly float _rectWidth;
        private readonly int _plotsPerRow;
        private readonly int _halfScreenPlots;

        /// <summary>
        /// Initializes a new SpriteRenderer for the specified surface and farm group
        /// </summary>
        /// <param name="surface">The text surface to draw on (LCD panel or cockpit screen)</param>
        /// <param name="farmGroup">Farm group containing farm plots to display</param>
        public SpriteRenderer(IMyTextSurface surface, FarmGroup farmGroup)
        {
            _surface = surface;
            _farmGroup = farmGroup;
            _viewport = surface.SurfaceSize;

            // Calculate derived layout values
            _iconSize = _viewport.Y / 8f * ICON_SCALE;
            _iconPadding = _iconSize / 2f;
            _leftMargin = SPACING;
            _waterRectHeight = _iconSize / 5f;
            _rectHeight = _viewport.Y / 8f - _waterRectHeight - SPACING;
            _rectWidth = _rectHeight * 0.75f;

            // Calculate how many plots fit per row (accounting for icon)
            float availableWidth = _viewport.X - _leftMargin - _iconSize - SPACING - SPACING;
            _plotsPerRow = (int)((availableWidth + SPACING) / (_rectWidth + SPACING));
            if (_plotsPerRow < 1)
                _plotsPerRow = 1;

            _halfScreenPlots = _plotsPerRow / 2 - 1;
        }

        /// <summary>
        /// Draws a graphical UI using sprites on the configured text surface
        /// </summary>
        public void DrawGraphicalUI()
        {
            _surface.ContentType = ContentType.SCRIPT;
            _surface.Script = string.Empty;
            // _surface.ScriptBackgroundColor = Color.Black;
            // _surface.ScriptForegroundColor = Color.White;

            var frame = _surface.DrawFrame();

            // Draw header and footer
            DrawHeader(frame);
            DrawFooter(frame);

            if (_farmGroup == null || _farmGroup.FarmPlots.Count == 0)
            {
                frame.Dispose();
                return;
            }

            // Group plots by PlantType, ordered by group size (largest first), empty plots last
            var allGroups = _farmGroup.FarmPlots.GroupBy(p => p.PlantType).ToList();

            var nonEmptyGroups = allGroups
                .Where(g => !string.IsNullOrEmpty(g.Key))
                .OrderByDescending(g => g.Count())
                .ToList();

            var emptyGroups = allGroups.Where(g => string.IsNullOrEmpty(g.Key)).ToList();

            var groupedPlots = nonEmptyGroups.Concat(emptyGroups).ToList();

            // Pre-process groups into layout rows (pair small groups side-by-side)
            var layoutRows = new List<LayoutRow>();
            IGrouping<string, FarmPlot> pendingGroup = null;

            foreach (var group in groupedPlots)
            {
                bool isSmallGroup = group.Count() <= _halfScreenPlots;

                if (isSmallGroup)
                {
                    if (pendingGroup == null)
                    {
                        // Save this small group as pending
                        pendingGroup = group;
                    }
                    else
                    {
                        // Pair with pending group
                        layoutRows.Add(
                            new LayoutRow { LeftGroup = pendingGroup, RightGroup = group }
                        );
                        pendingGroup = null;
                    }
                }
                else
                {
                    // Large group gets its own row
                    if (pendingGroup != null)
                    {
                        // Flush pending group first
                        layoutRows.Add(new LayoutRow { LeftGroup = pendingGroup });
                        pendingGroup = null;
                    }
                    layoutRows.Add(new LayoutRow { LeftGroup = group });
                }
            }

            // Flush any remaining pending group
            if (pendingGroup != null)
            {
                layoutRows.Add(new LayoutRow { LeftGroup = pendingGroup });
            }

            // Start just below the header (header is at y=10 with scale 0.8)
            float headerHeight = HEADER_SCALE * HEADER_FONT_HEIGHT; // Approximate font height with scale
            float currentY = 10f + headerHeight + SPACING * 3f;

            // Draw each layout row
            foreach (var layoutRow in layoutRows)
            {
                int maxRowsInThisLayoutRow = 0;

                // Process left column (always present)
                if (layoutRow.LeftGroup != null)
                {
                    int rowsInLeftGroup = DrawColumn(
                        frame,
                        layoutRow.LeftGroup,
                        _leftMargin,
                        currentY
                    );
                    maxRowsInThisLayoutRow = rowsInLeftGroup;
                }

                // Process right column (if present)
                if (layoutRow.RightGroup != null)
                {
                    int rowsInRightGroup = DrawColumn(
                        frame,
                        layoutRow.RightGroup,
                        _viewport.X / 2f + _leftMargin,
                        currentY
                    );
                    if (rowsInRightGroup > maxRowsInThisLayoutRow)
                    {
                        maxRowsInThisLayoutRow = rowsInRightGroup;
                    }
                }

                // Move to next layout row (based on tallest group in this row)
                currentY += maxRowsInThisLayoutRow * (_rectHeight + SPACING * 2f) + SPACING;
            }

            frame.Dispose();
        }

        /// <summary>
        /// Determines the appropriate color for a farm plot based on its plant state
        /// </summary>
        /// <param name="plot">The farm plot to evaluate</param>
        /// <returns>Color for the plot outline and growth indicator</returns>
        private Color GetPlotColor(FarmPlot plot)
        {
            if (_farmGroup.ProgrammableBlock == null)
            {
                return Color.Green;
            }

            if (plot.IsPlantPlanted)
            {
                if (plot.IsPlantAlive)
                {
                    if (plot.IsPlantFullyGrown)
                    {
                        return _farmGroup.ProgrammableBlock.PlantedReadyColor;
                    }
                    else
                    {
                        return _farmGroup.ProgrammableBlock.PlantedAliveColor;
                    }
                }
                else
                {
                    return _farmGroup.ProgrammableBlock.PlantedDeadColor;
                }
            }
            else
            {
                return _farmGroup.ProgrammableBlock.PlanterEmptyColor;
            }
        }

        /// <summary>
        /// Draws a column of farm plots with an optional group icon
        /// </summary>
        /// <param name="frame">The sprite frame to add sprites to</param>
        /// <param name="group">The group of plots to draw</param>
        /// <param name="columnStartX">X position where the column starts</param>
        /// <param name="currentY">Y position where the column starts</param>
        /// <returns>Number of rows used by this column</returns>
        private int DrawColumn(
            MySpriteDrawFrame frame,
            IGrouping<string, FarmPlot> group,
            float columnStartX,
            float currentY
        )
        {
            string plantType = group.Key;
            var plots = group.ToList();

            // Draw group icon
            if (!string.IsNullOrEmpty(plantType))
            {
                DrawGroupIcon(frame, columnStartX, currentY, plots);
            }

            // Draw plots in the group
            for (int i = 0; i < plots.Count; i++)
            {
                var plot = plots[i];
                int row = i / _plotsPerRow;
                int col = i % _plotsPerRow;

                // Calculate position (offset by icon)
                float plotStartX = columnStartX + (_iconSize + _iconPadding * 2f) / 2f;
                float x = plotStartX + col * (_rectWidth + SPACING) + _rectWidth / 2f;
                float y = currentY + row * (_rectHeight + SPACING * 2f);

                // Get growth progress and determine color
                float growthProgress = 0f;
                Color outlineColor = GetPlotColor(plot);

                if (plot.IsPlantPlanted && plot.IsPlantAlive)
                {
                    var details = plot.GetPlotDetails();
                    if (details != null)
                    {
                        growthProgress = details.GrowthProgress;
                    }
                }

                DrawFarmPlot(frame, x, y, plot, growthProgress, outlineColor);
            }

            // Return number of rows used
            return (plots.Count + _plotsPerRow - 1) / _plotsPerRow;
        }

        /// <summary>
        /// Draws the header sprite at the top of the screen
        /// </summary>
        /// <param name="frame">The sprite frame to add the header to</param>
        private void DrawHeader(MySpriteDrawFrame frame)
        {
            string headerText = GetHeaderAnimation(_farmGroup?.RunNumber ?? 0, true);
            var header = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = headerText,
                Position = new Vector2(_viewport.X / 2f, HEADER_Y_POSITION),
                RotationOrScale = HEADER_SCALE,
                Color = _surface.ScriptForegroundColor,
                Alignment = TextAlignment.CENTER,
                FontId = "White",
            };
            frame.Add(header);
        }

        /// <summary>
        /// Draws the footer sprite with dimensions at the bottom of the screen
        /// </summary>
        /// <param name="frame">The sprite frame to add the footer to</param>
        private void DrawFooter(MySpriteDrawFrame frame)
        {
            string dimensionsText = $"{(int)_viewport.Y} x {(int)_viewport.X}";
            var dimensions = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = dimensionsText,
                Position = new Vector2(_viewport.X / 2f, _viewport.Y - HEADER_Y_POSITION * 2f),
                RotationOrScale = HEADER_SCALE,
                Color = _surface.ScriptForegroundColor,
                Alignment = TextAlignment.CENTER,
                FontId = "White",
            };
            frame.Add(dimensions);
        }

        /// <summary>
        /// Draws a plant group icon at the specified position
        /// </summary>
        /// <param name="frame">The sprite frame to add the icon to</param>
        /// <param name="columnStartX">X position of the column start</param>
        /// <param name="currentY">Y position for the icon</param>
        /// <param name="plots">List of plots in the group</param>
        private void DrawGroupIcon(
            MySpriteDrawFrame frame,
            float columnStartX,
            float currentY,
            List<FarmPlot> plots
        )
        {
            if (plots.Count > 0 && plots[0].IsPlantPlanted)
            {
                float iconX = columnStartX + _iconPadding;
                float iconY = currentY;

                var groupIcon = new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = plots[0].PlantId,
                    Position = new Vector2(iconX, iconY),
                    Size = new Vector2(_iconSize, _iconSize),
                    Alignment = TextAlignment.CENTER,
                };
                frame.Add(groupIcon);
            }
        }

        /// <summary>
        /// Draws a single farm plot with growth and water indicators
        /// </summary>
        /// <param name="frame">The sprite frame to add the plot sprites to</param>
        /// <param name="x">X position of the plot center</param>
        /// <param name="y">Y position of the plot center</param>
        /// <param name="plot">The farm plot to draw</param>
        /// <param name="growthProgress">Growth progress value (0.0 to 1.0)</param>
        /// <param name="outlineColor">Color for the plot outline and growth indicator</param>
        private void DrawFarmPlot(
            MySpriteDrawFrame frame,
            float x,
            float y,
            FarmPlot plot,
            float growthProgress,
            Color outlineColor
        )
        {
            // Draw outline rectangle with state-based color
            var outline = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "SquareSimple",
                Position = new Vector2(x, y),
                Size = new Vector2(_rectWidth, _rectHeight),
                Color = outlineColor,
                Alignment = TextAlignment.CENTER,
            };
            frame.Add(outline);

            // Draw black background (creates padding inside border)
            float padding = PLOT_PADDING * 2f;
            var background = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "SquareSimple",
                Position = new Vector2(x, y),
                Size = new Vector2(_rectWidth - padding, _rectHeight - padding),
                Color = _surface.ScriptBackgroundColor,
                Alignment = TextAlignment.CENTER,
            };
            frame.Add(background);

            // Draw filled rectangle based on growth
            if (growthProgress > 0f)
            {
                float innerPadding = PLOT_PADDING * 4f;
                float plotAvailableHeight = _rectHeight - innerPadding;
                float filledHeight = plotAvailableHeight * growthProgress;
                float filledY = y + (_rectHeight - filledHeight - innerPadding) / 2f;

                var filled = new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = new Vector2(x, filledY),
                    Size = new Vector2(_rectWidth - innerPadding, filledHeight),
                    Color = outlineColor,
                    Alignment = TextAlignment.CENTER,
                };
                frame.Add(filled);
            }

            // Draw water level indicator below the farm plot
            float waterRectY = y + (_rectHeight / 2f) + _waterRectHeight - SPACING;

            // Water level outline (same color as farm plot)
            var waterOutline = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "SquareSimple",
                Position = new Vector2(x, waterRectY),
                Size = new Vector2(_rectWidth, _waterRectHeight),
                Color = outlineColor,
                Alignment = TextAlignment.CENTER,
            };
            frame.Add(waterOutline);

            // Water level background (black)
            var waterBackground = new MySprite()
            {
                Type = SpriteType.TEXTURE,
                Data = "SquareSimple",
                Position = new Vector2(x, waterRectY),
                Size = new Vector2(_rectWidth - padding, _waterRectHeight - padding),
                Color = Color.Black,
                Alignment = TextAlignment.CENTER,
            };
            frame.Add(waterBackground);

            // Water level fill bar (blue, left to right)
            float waterRatio = (float)plot.WaterFilledRatio;
            if (waterRatio > 0f)
            {
                float waterInnerPadding = padding + WATER_INNER_PADDING;
                float waterAvailableWidth = _rectWidth - waterInnerPadding;
                float filledWidth = waterAvailableWidth * waterRatio;
                float waterFilledX = x - (waterAvailableWidth / 2f) + (filledWidth / 2f);

                var waterFilled = new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = new Vector2(waterFilledX, waterRectY),
                    Size = new Vector2(filledWidth, _waterRectHeight - waterInnerPadding),
                    Color = Color.Blue,
                    Alignment = TextAlignment.CENTER,
                };
                frame.Add(waterFilled);
            }
        }

        /// <summary>
        /// Gets the animated header text based on run number
        /// </summary>
        /// <param name="runNumber">Current animation frame (0-2)</param>
        /// <returns>Animated header string</returns>
        private static string GetHeaderAnimation(int runNumber, bool doubleSided = false)
        {
            var title = "Farmhand";
            var animationStart = new[] { "––•", "–•–", "•––" };
            var animationEnd = new[] { "•––", "–•–", "––•" };

            var frame = animationEnd[runNumber % animationEnd.Length];

            return doubleSided
                ? $"{animationStart[runNumber % animationEnd.Length]} {title} {animationEnd[runNumber % animationEnd.Length]}"
                : $"{title} {animationEnd[runNumber % animationEnd.Length]}";
        }
    }
}
