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

        // Used to force redraw of sprites on server clients
        private readonly bool _shiftSprites;

        // Cached layout to avoid recalculation when plot list hasn't changed
        private List<LayoutRow> _cachedLayoutRows;
        private int _cachedPlotCount = -1;

        /// <summary>
        /// Initializes a new SpriteRenderer for the specified surface and farm group
        /// </summary>
        /// <param name="surface">The text surface to draw on (LCD panel or cockpit screen)</param>
        /// <param name="farmGroup">Farm group containing farm plots to display</param>
        /// <param name="customTitle">Optional custom title text (defaults to empty string)</param>
        /// <param name="shiftSprites">Whether to shift sprites for redraw on server clients</param>
        public SpriteRenderer(
            IMyTextSurface surface,
            FarmGroup farmGroup,
            string customTitle = "",
            bool shiftSprites = false
        )
        {
            _surface = surface;
            _farmGroup = farmGroup;
            _customTitle = customTitle;
            _shiftSprites = shiftSprites;
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
            // 512x1024 - Rotated Wide LCD panel
            // 512x362 - Sloped LCD panel
            // 362x512 - Rotated Sloped LCD panel
            if (
                (width == 512 && height == 512)
                || (width == 512 && height == 1024)
                || (width == 512 && height == 362)
                || (width == 362 && height == 512)
            )
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
            // 307x512 - Rotated Small LCD/Text panel
            if ((width == 512 && height == 307) || (width == 307 && height == 512))
            {
                return new ScreenLayout
                {
                    Spacing = 3,
                    HeaderFontHeight = 18,
                    HeaderTextScale = 0.7f,
                    IconSize = 42,
                    WaterRectHeight = 8,
                    GrowthRectHeight = 37,
                    RectWidth = 30,
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
                // Shift sprite array every other render to force redraw on server clients
                if (_shiftSprites)
                {
                    frame.Add(new MySprite());
                }

                // Draw header and footer
                DrawHeader(frame);

                // Get or compute layout rows (cached when plot count hasn't changed)
                var layoutRows = GetOrComputeLayoutRows();

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
        /// Gets or computes the layout rows, caching the result when plot count hasn't changed
        /// </summary>
        /// <returns>List of layout rows for rendering</returns>
        private List<LayoutRow> GetOrComputeLayoutRows()
        {
            int currentPlotCount = _farmGroup.FarmPlots.Count;

            // Return cached layout if plot count hasn't changed
            if (_cachedLayoutRows != null && _cachedPlotCount == currentPlotCount)
            {
                return _cachedLayoutRows;
            }

            // Recompute layout when plot count has changed
            _cachedPlotCount = currentPlotCount;

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

            // Cache the computed layout
            _cachedLayoutRows = layoutRows;
            return layoutRows;
        }

        /// <summary>
        /// Calculates all rendering colors for a farm plot in a single pass to avoid redundant checks
        /// </summary>
        /// <param name="plot">The farm plot to evaluate</param>
        /// <param name="isAlternateFrame">Whether this is an alternate frame for blinking effects</param>
        /// <returns>PlotRenderState containing all colors and growth progress</returns>
        private RenderHelpers.PlotRenderState CalculatePlotRenderState(
            FarmPlot plot,
            bool isAlternateFrame
        )
        {
            // Get plot details once for all checks
            var plotDetails = plot.GetPlotDetails();

            // Use shared helper method for color calculation
            return RenderHelpers.CalculatePlotRenderState(
                plot,
                plotDetails,
                _farmGroup.ProgrammableBlock,
                isAlternateFrame
            );
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
            DrawGroupIcon(frame, columnStartX, currentY, plots);

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

                // Calculate all rendering state once for this plot
                RenderHelpers.PlotRenderState renderState = CalculatePlotRenderState(
                    plot,
                    isAlternateFrame
                );

                DrawFarmPlot(frame, x, y, plot, renderState);
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
            string headerText = RenderHelpers.GetHeaderAnimation(
                _farmGroup?.RunNumber ?? 0,
                headerTitle,
                TextAlignment.CENTER
            );
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
            if (plots.Count > 0)
            {
                float iconX = columnStartX + (_iconSize / 2);
                float iconY = currentY;

                if (plots[0].IsPlantPlanted)
                {
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
                else
                {
                    frame.Add(
                        new MySprite()
                        {
                            Type = SpriteType.TEXTURE,
                            Data = "Circle",
                            Position = CreatePosition(iconX + 1, iconY),
                            Size = new Vector2(_iconSize * 1 / 3, _iconSize * 1 / 3),
                            Color = _farmGroup.ProgrammableBlock.PlanterEmptyColor,
                            Alignment = TextAlignment.CENTER,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Draws a single farm plot with growth and water indicators
        /// </summary>
        /// <param name="frame">The sprite frame to add the plot sprites to</param>
        /// <param name="x">X position of the plot center</param>
        /// <param name="y">Y position of the plot center</param>
        /// <param name="plot">The farm plot to draw</param>
        /// <param name="renderState">Pre-calculated rendering state with colors and growth progress</param>
        private void DrawFarmPlot(
            MySpriteDrawFrame frame,
            float x,
            float y,
            FarmPlot plot,
            RenderHelpers.PlotRenderState renderState
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
                    Color = renderState.OutlineColor,
                    Alignment = TextAlignment.CENTER,
                }
            );

            // Draw background for growth section
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
            if (renderState.GrowthProgress > 0f)
            {
                // Use the same available height as the background
                float filledHeight =
                    (growthAvailableHeight - 2 * _plotPadding) * renderState.GrowthProgress;
                float filledY = y + (_growthRectHeight - filledHeight - innerPadding) / 2;

                frame.Add(
                    new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Position = CreatePosition(x, filledY - growthYOffet + (_plotPadding / 2)),
                        Size = new Vector2(_rectWidth - innerPadding, filledHeight),
                        Color = renderState.ProgressBarColor,
                        Alignment = TextAlignment.CENTER,
                    }
                );
            }

            // Draw water level indicator below the farm plot
            // Available height = WaterRectHeight - 2x padding (top and bottom, matching sides)
            float waterAvailableHeight = _waterRectHeight - _plotPadding;

            // Vertical position for water rectangle
            float waterRectY = y + (RectHeight / 2) - (_waterRectHeight / 2) - _plotPadding / 2;

            // Water level background
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
                        Color = renderState.WaterBarColor,
                        Alignment = TextAlignment.CENTER,
                    }
                );
            }
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
