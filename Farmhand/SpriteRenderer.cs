using System;
using System.Collections;
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
        private readonly int _firstRowTop;

        // Computed property: Total rect height is growth + water
        private int RectHeight => _growthRectHeight + _waterRectHeight;

        // Used to force redraw of sprites on server clients
        private readonly bool _shiftSprites;

        // Cached layout to avoid recalculation when plot list hasn't changed
        private List<LayoutRow> _cachedLayoutRows;
        private int _cachedPlotCount = -1;

        /// <summary>
        /// Plots drawn between chunk boundaries, matching ScanChunkSize in Program.Scan.cs so
        /// the two per-plot loops in the pipeline cost about the same per chunk.
        /// </summary>
        private const int PlotsPerChunk = 20;

        // Reusable sprite buffer for DrawGraphicalUI, so that path allocates nothing per render
        private readonly List<MySprite> _spriteBuffer = new List<MySprite>();

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
            // First tile row sits below the header with quadruple spacing, or at the top
            // margin when the layout dropped the header
            _firstRowTop =
                _headerFontHeight > 0 ? _headerFontHeight + _spacing * 4 : _spacing;
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
            // Scale from width so 512-wide screens keep the hand-tuned sizes and 1024-wide
            // screens are capped to them. The floor keeps a tile 12px wide with a 1px border
            // and a 1px water fill. Every dimension is rounded once here; positions are then
            // built by summing these integers, so every sprite edge lands on a pixel boundary.
            float s = Math.Min(1f, Math.Max(0.4f, width / 512f));
            var layout = new ScreenLayout
            {
                Spacing = Scale(6, s),
                HeaderFontHeight = Scale(30, s),
                HeaderTextScale = s,
                IconSize = Scale(50, s),
                WaterRectHeight = Scale(10, s),
                GrowthRectHeight = Scale(40, s),
                RectWidth = Scale(30, s),
                LeftMargin = Scale(10, s),
                PlotPadding = Math.Max(1, Scale(2, s)),
            };

            // Vertical budget: drop the header when it and one tile row do not fit, then
            // shorten the growth bar so a single row fits. A 512x73 corner LCD becomes one
            // strip of 30x61 tiles this way.
            int tileHeight = layout.GrowthRectHeight + layout.WaterRectHeight;
            if (layout.HeaderFontHeight + layout.Spacing * 5 + tileHeight > height)
            {
                layout.HeaderFontHeight = 0;
            }
            int maxTileHeight = height - layout.Spacing * 2;
            if (tileHeight > maxTileHeight)
            {
                layout.GrowthRectHeight = maxTileHeight - layout.WaterRectHeight;
                tileHeight = maxTileHeight;
            }
            layout.IconSize = Math.Min(layout.IconSize, tileHeight);

            // Supported when the growth fill can be at least 1px and one tile fits beside the icon
            layout.IsSupported =
                layout.GrowthRectHeight >= layout.PlotPadding * 4 + 1
                && width
                    >= layout.LeftMargin
                        + layout.IconSize
                        + layout.Spacing * 2
                        + layout.RectWidth;
            return layout;
        }

        /// <summary>
        /// Scales a 512-wide reference dimension and rounds it to a whole pixel
        /// </summary>
        private static int Scale(int reference, float s)
        {
            return (int)Math.Round(reference * s);
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
            PrepareSurface();
            _spriteBuffer.Clear();
            // Drains the builder in one call. This path is reached from TextSurfaceProvider,
            // which has no pipeline step of its own to spread the work across, so its
            // graphical screens still build as a single unyielded unit.
            IEnumerator build = BuildSprites(_spriteBuffer);
            while (build.MoveNext()) { }
            if (_spriteBuffer.Count == 0) return;

            using (var frame = _surface.DrawFrame())
            {
                for (int i = 0; i < _spriteBuffer.Count; i++)
                {
                    frame.Add(_spriteBuffer[i]);
                }
            }
        }

        /// <summary>
        /// Prepares the surface for script rendering. Must run even for unsupported screens,
        /// matching the original DrawGraphicalUI which set these before its unsupported check.
        /// </summary>
        public void PrepareSurface()
        {
            _surface.ContentType = ContentType.SCRIPT;
            _surface.Script = string.Empty;
        }

        /// <summary>
        /// Accumulates this panel's sprites into a buffer without touching the surface, so the
        /// expensive layout work can be spread across ticks. The parameter is named frame
        /// because the helpers it delegates to were written against a draw frame; it is an
        /// ordinary list here.
        /// </summary>
        public IEnumerator BuildSprites(List<MySprite> frame)
        {
            if (!_isScreenSizeSupported)
            {
                DrawUnsupportedScreenMessage(frame);
                yield break;
            }

            if (_farmGroup == null || _farmGroup.FarmPlots.Count == 0)
            {
                yield break;
            }

            // Shift sprite array every other render to force redraw on server clients
            if (_shiftSprites)
            {
                frame.Add(new MySprite());
            }

            // Draw header and footer
            DrawHeader(frame);

            // Get or compute layout rows (cached when plot count hasn't changed)
            var layoutRows = GetOrComputeLayoutRows();

            int currentTop = _firstRowTop;

            // Draw each layout row. Indexed rather than foreach, matching how _groupSnapshot is
            // walked in the pipeline steps: an enumerator suspended across ticks throws if its
            // list is mutated. GetOrComputeLayoutRows returns a freshly built list today, so
            // foreach would also be safe, but the idiom is what keeps that from mattering.
            for (int r = 0; r < layoutRows.Count; r++)
            {
                LayoutRow layoutRow = layoutRows[r];
                int maxRowsInThisLayoutRow = 0;

                // Process left column (always present)
                if (layoutRow.LeftGroup != null)
                {
                    List<FarmPlot> leftPlots = layoutRow.LeftGroup.ToList();
                    maxRowsInThisLayoutRow = RowsForColumn(leftPlots.Count);
                    IEnumerator left = DrawColumn(frame, leftPlots, _leftMargin, currentTop);
                    while (left.MoveNext())
                    {
                        yield return null;
                    }
                }

                // Process right column (if present)
                if (layoutRow.RightGroup != null)
                {
                    List<FarmPlot> rightPlots = layoutRow.RightGroup.ToList();
                    int rowsInRightGroup = RowsForColumn(rightPlots.Count);
                    if (rowsInRightGroup > maxRowsInThisLayoutRow)
                    {
                        maxRowsInThisLayoutRow = rowsInRightGroup;
                    }
                    IEnumerator right = DrawColumn(
                        frame,
                        rightPlots,
                        (int)_viewport.Width / 2 + _leftMargin,
                        currentTop
                    );
                    while (right.MoveNext())
                    {
                        yield return null;
                    }
                }

                // Move to next layout row (based on tallest group in this row, with double spacing)
                currentTop +=
                    maxRowsInThisLayoutRow * (_growthRectHeight + _spacing * 2) + _spacing * 2;

                yield return null;
            }
        }

        /// <summary>
        /// Rows a column of the given plot count occupies. A pure function of the count, which
        /// is why DrawColumn no longer returns it: an iterator cannot return a value, and the
        /// caller needs this before the column has finished drawing.
        /// </summary>
        private int RowsForColumn(int plotCount)
        {
            return (plotCount + _plotsPerRow - 1) / _plotsPerRow;
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
        /// Draws a column of farm plots with an optional group icon, yielding every
        /// PlotsPerChunk plots so a large single-plant group does not become one
        /// uninterruptible chunk. Use RowsForColumn for the row count.
        /// </summary>
        /// <param name="frame">The sprite frame to add sprites to</param>
        /// <param name="plots">The plots to draw, already materialised by the caller</param>
        /// <param name="columnStartX">X position where the column starts</param>
        /// <param name="currentY">Y position where the column starts</param>
        private IEnumerator DrawColumn(
            List<MySprite> frame,
            List<FarmPlot> plots,
            int columnLeft,
            int rowTop
        )
        {
            // Draw group icon
            DrawGroupIcon(frame, columnLeft, rowTop, plots);

            // Calculate if this is an alternate frame for blinking (odd frames: 1, 3, 5)
            bool isAlternateFrame = (_farmGroup.RunNumber % 2) == 1;

            // Draw plots in the group
            int plotLeft = columnLeft + _iconSize + _spacing;
            for (int i = 0; i < plots.Count; i++)
            {
                var plot = plots[i];
                int row = i / _plotsPerRow;
                int col = i % _plotsPerRow;

                int left = plotLeft + col * (_rectWidth + _spacing);
                int top = rowTop + row * (RectHeight + _spacing * 2);

                // Calculate all rendering state once for this plot
                RenderHelpers.PlotRenderState renderState = CalculatePlotRenderState(
                    plot,
                    isAlternateFrame
                );

                DrawFarmPlot(frame, left, top, plot, renderState);

                if ((i + 1) % PlotsPerChunk == 0)
                {
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Draws the header sprite at the top of the screen
        /// </summary>
        /// <param name="frame">The sprite frame to add the header to</param>
        private void DrawHeader(List<MySprite> frame)
        {
            // The layout drops the header on screens too short to fit it and a tile row
            if (_headerFontHeight == 0) return;

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
            List<MySprite> frame,
            int columnLeft,
            int rowTop,
            List<FarmPlot> plots
        )
        {
            if (plots.Count > 0)
            {
                // Icon is vertically centred on the first tile row
                int iconTop = rowTop + (RectHeight - _iconSize) / 2;

                if (plots[0].IsPlantPlanted)
                {
                    frame.Add(
                        new MySprite()
                        {
                            Type = SpriteType.TEXTURE,
                            Data = RenderHelpers.ResolveColorfulIconSprite(
                                plots[0].PlantId,
                                _surface
                            ),
                            Position = CreatePosition(
                                columnLeft + _iconSize / 2f,
                                iconTop + _iconSize / 2f
                            ),
                            Size = new Vector2(_iconSize, _iconSize),
                            Alignment = TextAlignment.CENTER,
                        }
                    );
                }
                else
                {
                    int circleSize = _iconSize / 3;
                    int inset = (_iconSize - circleSize) / 2;
                    AddRect(
                        frame,
                        "Circle",
                        columnLeft + inset + 1,
                        iconTop + inset,
                        circleSize,
                        circleSize,
                        _farmGroup.ProgrammableBlock.PlanterEmptyColor
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
            List<MySprite> frame,
            int left,
            int top,
            FarmPlot plot,
            RenderHelpers.PlotRenderState renderState
        )
        {
            // All rects are integer left/top/width/height, inset from the outline by whole
            // borders, so the border is the same width on every side at every screen size.
            int pad = _plotPadding;
            int innerLeft = left + pad;
            int innerWidth = _rectWidth - 2 * pad;
            int fillLeft = innerLeft + pad;
            int fillWidth = innerWidth - 2 * pad;

            // Outline rectangle with state-based color
            AddRect(frame, "SquareSimple", left, top, _rectWidth, RectHeight, renderState.OutlineColor);

            // Growth background, one border inside the outline
            int growthBgTop = top + pad;
            int growthBgHeight = _growthRectHeight - 2 * pad;
            AddRect(
                frame,
                "SquareSimple",
                innerLeft,
                growthBgTop,
                innerWidth,
                growthBgHeight,
                _surface.ScriptBackgroundColor
            );

            // Growth fill, one border inside the background, growing up from the bottom
            int growthFillMax = growthBgHeight - 2 * pad;
            int growthFillHeight = (int)Math.Round(growthFillMax * renderState.GrowthProgress);
            if (growthFillHeight > 0)
            {
                AddRect(
                    frame,
                    "SquareSimple",
                    fillLeft,
                    growthBgTop + pad + growthFillMax - growthFillHeight,
                    fillWidth,
                    growthFillHeight,
                    renderState.ProgressBarColor
                );
            }

            // Water background below the growth section, separated by one border
            int waterBgTop = growthBgTop + growthBgHeight + pad;
            int waterBgHeight = _waterRectHeight - pad;
            AddRect(
                frame,
                "SquareSimple",
                innerLeft,
                waterBgTop,
                innerWidth,
                waterBgHeight,
                _surface.ScriptBackgroundColor
            );

            // Water fill, one border inside the background, growing left to right
            int waterFillWidth = (int)Math.Round(fillWidth * (float)plot.WaterFilledRatio);
            if (waterFillWidth > 0)
            {
                AddRect(
                    frame,
                    "SquareSimple",
                    fillLeft,
                    waterBgTop + pad,
                    waterFillWidth,
                    waterBgHeight - 2 * pad,
                    renderState.WaterBarColor
                );
            }
        }

        /// <summary>
        /// Adds a texture sprite from an integer left/top/width/height rect. The centre may
        /// land on a half pixel for odd sizes; the edges are what get rasterised and those
        /// are always whole pixels.
        /// </summary>
        private void AddRect(
            List<MySprite> frame,
            string data,
            int left,
            int top,
            int width,
            int height,
            Color color
        )
        {
            frame.Add(
                new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = data,
                    Position = CreatePosition(left + width / 2f, top + height / 2f),
                    Size = new Vector2(width, height),
                    Color = color,
                    Alignment = TextAlignment.CENTER,
                }
            );
        }

        /// <summary>
        /// Draws an unsupported screen size message in the center of the screen
        /// </summary>
        /// <param name="frame">The sprite frame to add the message to</param>
        private void DrawUnsupportedScreenMessage(List<MySprite> frame)
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
