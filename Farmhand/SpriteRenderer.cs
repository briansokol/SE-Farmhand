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
    /// Screen-size-specific layout configuration with integer pixel values
    /// </summary>
    internal struct ScreenLayout
    {
        public int Spacing;
        public int HeaderFontHeight;
        public float HeaderTextScale;
        public int IconSize;
        public int WaterRectHeight;
        public int GrowthRectHeight;
        public int RectWidth;
        public int LeftMargin;
        public int PlotPadding;
        public bool IsSupported;
    }

    /// <summary>
    /// Provides sprite-based graphical rendering for text surfaces
    /// </summary>
    internal class SpriteRenderer
    {
        private const bool DEBUG = false;

        // Instance fields
        private readonly IMyTextSurface _surface;
        private readonly FarmGroup _farmGroup;
        private readonly string _customTitle;
        private readonly RectangleF _viewport;
        private readonly Vector2 _textureSize;
        private readonly bool _isScreenSizeSupported;

        // Layout values (integer-based for pixel-perfect rendering)
        private readonly int _spacing;
        private readonly int _headerYPosition;
        private readonly int _headerFontHeight;
        private readonly float _headerTextScale;
        private readonly int _iconSize;
        private readonly int _leftMargin;
        private readonly int _waterRectHeight;
        private readonly int _growthRectHeight;
        private readonly int _rectWidth;
        private readonly int _plotPadding;
        private readonly int _plotsPerRow;
        private readonly int _halfScreenPlots;

        // Computed property: Total rect height is growth + water
        private int RectHeight => _growthRectHeight + _waterRectHeight;

        /// <summary>
        /// Initializes a new SpriteRenderer for the specified surface and farm group
        /// </summary>
        /// <param name="surface">The text surface to draw on (LCD panel or cockpit screen)</param>
        /// <param name="farmGroup">Farm group containing farm plots to display</param>
        /// <param name="customTitle">Optional custom title text (defaults to empty string)</param>
        public SpriteRenderer(IMyTextSurface surface, FarmGroup farmGroup, string customTitle = "")
        {
            _surface = surface;
            _farmGroup = farmGroup;
            _customTitle = customTitle;
            _textureSize = _surface.TextureSize;
            _viewport = new RectangleF(
                (_textureSize - _surface.SurfaceSize) / 2f,
                _surface.SurfaceSize
            );

            // Get integer-based layout for the screen size
            ScreenLayout layout = GetLayoutForScreenSize(
                (int)_viewport.Width,
                (int)_viewport.Height
            );

            // Track if screen size is supported
            _isScreenSizeSupported = layout.IsSupported;

            // Initialize layout values with integers for pixel-perfect rendering
            _spacing = layout.Spacing;
            _headerFontHeight = layout.HeaderFontHeight;
            _headerTextScale = layout.HeaderTextScale;
            _headerYPosition = _headerFontHeight / 2;
            _iconSize = layout.IconSize;
            _leftMargin = layout.LeftMargin;
            _waterRectHeight = layout.WaterRectHeight;
            _growthRectHeight = layout.GrowthRectHeight;
            _rectWidth = layout.RectWidth;
            _plotPadding = layout.PlotPadding;

            // Calculate how many plots fit per row (accounting for icon)
            int availableWidth =
                (int)_viewport.Width - _leftMargin - _iconSize - _spacing - _spacing;
            _plotsPerRow = (availableWidth + _spacing) / (_rectWidth + _spacing);
            if (_plotsPerRow < 1)
                _plotsPerRow = 1;

            _halfScreenPlots = _plotsPerRow / 2 - 1;
        }

        /// <summary>
        /// Gets the appropriate layout configuration for the given screen size
        /// </summary>
        /// <param name="width">Screen width in pixels</param>
        /// <param name="height">Screen height in pixels</param>
        /// <returns>Screen-specific layout with integer pixel values</returns>
        private static ScreenLayout GetLayoutForScreenSize(int width, int height)
        {
            // 1024x512 - Wide LCD panel
            if (width == 1024 && height == 512)
            {
                return new ScreenLayout
                {
                    Spacing = 6,
                    HeaderFontHeight = 30,
                    HeaderTextScale = 1.0f,
                    IconSize = 50,
                    WaterRectHeight = 10,
                    GrowthRectHeight = 40,
                    RectWidth = 30,
                    LeftMargin = 10,
                    PlotPadding = 2,
                    IsSupported = true,
                };
            }

            // 512x512 - Square LCD panel
            if (width == 512 && height == 512)
            {
                return new ScreenLayout
                {
                    Spacing = 6,
                    HeaderFontHeight = 30,
                    HeaderTextScale = 1.0f,
                    IconSize = 50,
                    WaterRectHeight = 10,
                    GrowthRectHeight = 40,
                    RectWidth = 30,
                    LeftMargin = 10,
                    PlotPadding = 2,
                    IsSupported = true,
                };
            }

            // 512x307 - Small LCD/Text panel
            if (width == 512 && height == 307)
            {
                return new ScreenLayout
                {
                    Spacing = 4,
                    HeaderFontHeight = 18,
                    HeaderTextScale = 0.7f,
                    IconSize = 44,
                    WaterRectHeight = 8,
                    GrowthRectHeight = 36,
                    RectWidth = 26,
                    LeftMargin = 6,
                    PlotPadding = 2,
                    IsSupported = true,
                };
            }

            // Unsupported screen size - return minimal layout for error display
            return new ScreenLayout
            {
                Spacing = 10,
                HeaderFontHeight = 30,
                HeaderTextScale = 1.0f,
                IconSize = 0,
                WaterRectHeight = 0,
                GrowthRectHeight = 0,
                RectWidth = 0,
                LeftMargin = 0,
                PlotPadding = 0,
                IsSupported = false,
            };
        }

        /// <summary>
        /// Creates a Vector2 position with viewport offset applied
        /// </summary>
        /// <param name="x">X coordinate relative to drawable surface</param>
        /// <param name="y">Y coordinate relative to drawable surface</param>
        /// <returns>Vector2 with viewport offset applied for proper centering</returns>
        private Vector2 CreatePosition(float x, float y)
        {
            return new Vector2(x, y) + _viewport.Position;
        }

        /// <summary>
        /// Draws a graphical UI using sprites on the configured text surface
        /// </summary>
        public void DrawGraphicalUI()
        {
            _surface.ContentType = ContentType.SCRIPT;
            _surface.Script = string.Empty;

            // Check if screen size is unsupported
            if (!_isScreenSizeSupported)
            {
                using (var frame = _surface.DrawFrame())
                {
                    DrawUnsupportedScreenMessage(frame);
                }
                return;
            }

            if (_farmGroup == null || _farmGroup.FarmPlots.Count == 0)
            {
                return;
            }

            using (var frame = _surface.DrawFrame())
            {
                // Draw header and footer
                DrawHeader(frame);
                DrawFooter(frame);

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

                // Start just below the header (with double spacing)
                float currentY = RectHeight / 2 + _headerFontHeight + _spacing * 4;

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
                            _viewport.Width / 2f + _leftMargin,
                            currentY
                        );
                        if (rowsInRightGroup > maxRowsInThisLayoutRow)
                        {
                            maxRowsInThisLayoutRow = rowsInRightGroup;
                        }
                    }

                    // Move to next layout row (based on tallest group in this row, with double spacing)
                    currentY +=
                        maxRowsInThisLayoutRow * (_growthRectHeight + _spacing * 2) + _spacing * 2;
                }
            }
        }

        /// <summary>
        /// Determines the appropriate color for a farm plot based on its plant state
        /// </summary>
        /// <param name="plot">The farm plot to evaluate</param>
        /// <param name="isAlternateFrame">Whether this is an alternate frame for blinking effects</param>
        /// <returns>Color for the plot outline and growth indicator</returns>
        private Color GetPlotColor(FarmPlot plot, bool isAlternateFrame)
        {
            if (_farmGroup.ProgrammableBlock == null)
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
                && plotDetails.CropHealth < _farmGroup.ProgrammableBlock.HealthLowThreshold;

            if (isLowHealth && isAlternateFrame)
            {
                return _farmGroup.ProgrammableBlock.PlantedDeadColor;
            }

            // Check if plot has low water and should show warning color on alternate frames
            bool isLowWater =
                plot.IsFunctional()
                && plot.WaterFilledRatio <= _farmGroup.ProgrammableBlock.WaterLowThreshold
                && !isLowHealth; // Only show water warning if health is not low

            if (isLowWater && isAlternateFrame)
            {
                return _farmGroup.ProgrammableBlock.WaterLowColor;
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
        /// Determines the appropriate color for a farm plot's progress bar based on its plant state
        /// Progress bar stays solid (no blinking) when health is low
        /// </summary>
        /// <param name="plot">The farm plot to evaluate</param>
        /// <param name="isAlternateFrame">Whether this is an alternate frame for blinking effects</param>
        /// <returns>Color for the growth progress bar fill</returns>
        private Color GetProgressBarColor(FarmPlot plot, bool isAlternateFrame)
        {
            if (_farmGroup.ProgrammableBlock == null)
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
                && plotDetails.CropHealth < _farmGroup.ProgrammableBlock.HealthLowThreshold;

            // Check if plot has low water (only matters if health is OK)
            bool isLowWater =
                plot.IsFunctional()
                && plot.WaterFilledRatio <= _farmGroup.ProgrammableBlock.WaterLowThreshold
                && !isLowHealth; // Only show water warning if health is not low

            if (plot.IsPlantPlanted)
            {
                if (plot.IsPlantAlive)
                {
                    // Priority: Ready color > Low health warning > Growing color
                    if (plot.IsPlantFullyGrown)
                    {
                        // Plant is ready - show ready color even if health is low
                        return _farmGroup.ProgrammableBlock.PlantedReadyColor;
                    }
                    else if (isLowHealth)
                    {
                        // Plant is growing but health is low - show dead color (solid)
                        return _farmGroup.ProgrammableBlock.PlantedDeadColor;
                    }
                    else if (isLowWater && isAlternateFrame)
                    {
                        // Plant is growing, health OK, but water low - blink water warning
                        return _farmGroup.ProgrammableBlock.WaterLowColor;
                    }
                    else
                    {
                        // Plant is growing normally
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

            // Calculate if this is an alternate frame for blinking (odd frames: 1, 3, 5)
            bool isAlternateFrame = (_farmGroup.RunNumber % 2) == 1;

            // Draw plots in the group
            for (int i = 0; i < plots.Count; i++)
            {
                var plot = plots[i];
                int row = i / _plotsPerRow;
                int col = i % _plotsPerRow;

                // Calculate position (offset by icon)
                float plotStartX = columnStartX + _iconSize + _spacing;
                float x = plotStartX + col * (_rectWidth + _spacing) + _rectWidth / 2;
                float y = currentY + row * (RectHeight + _spacing * 2);

                // Get growth progress and determine colors
                float growthProgress = 0f;
                Color outlineColor = GetPlotColor(plot, isAlternateFrame);
                Color progressBarColor = GetProgressBarColor(plot, isAlternateFrame);

                if (plot.IsPlantPlanted && plot.IsPlantAlive)
                {
                    var details = plot.GetPlotDetails();
                    if (details != null)
                    {
                        growthProgress = details.GrowthProgress;
                    }
                }

                DrawFarmPlot(frame, x, y, plot, growthProgress, outlineColor, progressBarColor);
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
            var headerTitle = string.IsNullOrEmpty(_customTitle) ? "Farmhand" : _customTitle;
            string headerText = GetHeaderAnimation(_farmGroup?.RunNumber ?? 0, headerTitle);
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = headerText,
                    Position = CreatePosition(_viewport.Width / 2f, _headerYPosition),
                    RotationOrScale = _headerTextScale,
                    Color = _surface.ScriptForegroundColor,
                    Alignment = TextAlignment.CENTER,
                    FontId = "White",
                }
            );
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
                float iconX = columnStartX + (_iconSize / 2);
                float iconY = currentY;

                frame.Add(
                    new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = plots[0].PlantId,
                        Position = CreatePosition(iconX, iconY),
                        Size = new Vector2(_iconSize, _iconSize),
                        Alignment = TextAlignment.CENTER,
                    }
                );
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
        /// <param name="outlineColor">Color for the plot outline</param>
        /// <param name="progressBarColor">Color for the growth progress bar fill</param>
        private void DrawFarmPlot(
            MySpriteDrawFrame frame,
            float x,
            float y,
            FarmPlot plot,
            float growthProgress,
            Color outlineColor,
            Color progressBarColor
        )
        {
            // Calculate vertical offset for growth section (half of water height + padding)
            var growthYOffet = (_waterRectHeight + _plotPadding) / 2;

            // Draw outline rectangle with state-based color
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = CreatePosition(x, y),
                    Size = new Vector2(_rectWidth, RectHeight),
                    Color = outlineColor,
                    Alignment = TextAlignment.CENTER,
                }
            );

            // Draw black background for growth section
            // Available height = GrowthRectHeight - 2x padding (top and bottom)
            float growthAvailableHeight = _growthRectHeight - (2 * _plotPadding);
            // Shift down by half padding to center the padded background within the outline
            float backgroundY = y - growthYOffet + (_plotPadding / 2);
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = CreatePosition(x, backgroundY),
                    Size = new Vector2(_rectWidth - (2 * _plotPadding), growthAvailableHeight),
                    Color = _surface.ScriptBackgroundColor,
                    Alignment = TextAlignment.CENTER,
                }
            );

            float innerPadding = _plotPadding * 4;

            // Draw filled rectangle based on growth
            if (growthProgress > 0f)
            {
                // Use the same available height as the background
                float filledHeight = (growthAvailableHeight - 2 * _plotPadding) * growthProgress;
                float filledY = y + (_growthRectHeight - filledHeight - innerPadding) / 2;

                frame.Add(
                    new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Position = CreatePosition(x, filledY - growthYOffet + (_plotPadding / 2)),
                        Size = new Vector2(_rectWidth - innerPadding, filledHeight),
                        Color = progressBarColor,
                        Alignment = TextAlignment.CENTER,
                    }
                );
            }

            // Draw water level indicator below the farm plot
            // Available height = WaterRectHeight - 2x padding (top and bottom, matching sides)
            float waterAvailableHeight = _waterRectHeight - _plotPadding;

            // Vertical position for water rectangle
            float waterRectY = y + (RectHeight / 2) - (_waterRectHeight / 2) - _plotPadding / 2;

            // Water level background (black)
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = CreatePosition(x, waterRectY),
                    Size = new Vector2(_rectWidth - (2 * _plotPadding), waterAvailableHeight),
                    Color = _surface.ScriptBackgroundColor,
                    Alignment = TextAlignment.CENTER,
                }
            );

            // Water level fill bar (left to right)
            float waterRatio = (float)plot.WaterFilledRatio;
            if (waterRatio > 0f)
            {
                // Determine water bar color (always solid, shows water warning or base plant status)
                Color waterBarColor =
                    plot.WaterFilledRatio <= _farmGroup.ProgrammableBlock.WaterLowThreshold
                        ? _farmGroup.ProgrammableBlock.WaterLowColor
                        : GetPlotColor(plot, false); // Get base plant status color (no blinking)

                // Use consistent padding with growth bar
                float waterAvailableWidth = _rectWidth - innerPadding;
                float filledWidth = waterAvailableWidth * waterRatio;
                float waterFilledX = x - (waterAvailableWidth / 2) + (filledWidth / 2);

                frame.Add(
                    new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Position = CreatePosition(waterFilledX, waterRectY),
                        Size = new Vector2(filledWidth, waterAvailableHeight - (2 * _plotPadding)),
                        Color = waterBarColor,
                        Alignment = TextAlignment.CENTER,
                    }
                );
            }
        }

        /// <summary>
        /// Gets the animated header text based on run number
        /// </summary>
        /// <param name="runNumber">Current animation frame (0-5)</param>
        /// <param name="title">The title text to display in the header (defaults to "Farmhand")</param>
        /// <returns>Animated header string</returns>
        private static string GetHeaderAnimation(int runNumber, string title = "Farmhand")
        {
            var animationStart = new[] { "––•", "–•–", "•––" };
            var animationEnd = new[] { "•––", "–•–", "––•" };
            var frameNumber = runNumber > 2 ? runNumber - 3 : runNumber;

            return $"{animationStart[frameNumber % animationStart.Length]} {title} {animationEnd[frameNumber % animationEnd.Length]}";
        }

        /// <summary>
        /// Draws the footer sprite with dimensions at the bottom of the screen
        /// </summary>
        /// <param name="frame">The sprite frame to add the footer to</param>
        private void DrawFooter(MySpriteDrawFrame frame)
        {
            string dimensionsText = $"{(int)_viewport.Width} x {(int)_viewport.Height}";
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = dimensionsText,
                    Position = CreatePosition(
                        _viewport.Width / 2f,
                        _viewport.Height - _headerYPosition * 2f
                    ),
                    RotationOrScale = _headerTextScale,
                    Color = _surface.ScriptForegroundColor,
                    Alignment = TextAlignment.CENTER,
                    FontId = "White",
                }
            );
        }

        /// <summary>
        /// Draws an unsupported screen size message in the center of the screen
        /// </summary>
        /// <param name="frame">The sprite frame to add the message to</param>
        private void DrawUnsupportedScreenMessage(MySpriteDrawFrame frame)
        {
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = "Screen Size Unsupported",
                    Position = CreatePosition(_viewport.Width / 2f, _spacing),
                    RotationOrScale = 0.7f,
                    Color = _surface.ScriptForegroundColor,
                    Alignment = TextAlignment.CENTER,
                    FontId = "White",
                }
            );

            // Also show the dimensions below the message
            string dimensionsText = $"{(int)_viewport.Width} x {(int)_viewport.Height}";
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXT,
                    Data = dimensionsText,
                    Position = CreatePosition(
                        _viewport.Width / 2f,
                        _viewport.Height - 20f - _spacing
                    ),
                    RotationOrScale = 0.5f,
                    Color = _surface.ScriptForegroundColor,
                    Alignment = TextAlignment.CENTER,
                    FontId = "White",
                }
            );
        }
    }
}
