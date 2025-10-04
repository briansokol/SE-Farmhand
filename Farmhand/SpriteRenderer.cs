using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Provides sprite-based graphical rendering for text surfaces
    /// </summary>
    internal static class SpriteRenderer
    {
        /// <summary>
        /// Draws a graphical UI using sprites on the specified text surface
        /// </summary>
        /// <param name="surface">The text surface to draw on (LCD panel or cockpit screen)</param>
        /// <param name="farmGroup">Farm group containing farm plots to display</param>
        public static void DrawGraphicalUI(IMyTextSurface surface, FarmGroup farmGroup)
        {
            surface.ContentType = ContentType.SCRIPT;
            surface.Script = string.Empty;
            // surface.ScriptBackgroundColor = Color.Black;
            // surface.ScriptForegroundColor = Color.White;

            var viewport = surface.SurfaceSize;
            var frame = surface.DrawFrame();

            // Draw header centered at top of screen
            string headerText = GetHeaderAnimation(farmGroup?.RunNumber ?? 0, true);
            var header = new MySprite()
            {
                Type = SpriteType.TEXT,
                Data = headerText,
                Position = new Vector2(viewport.X / 2f, 10f),
                RotationOrScale = 0.8f,
                Color = surface.ScriptForegroundColor,
                Alignment = TextAlignment.CENTER,
                FontId = "White",
            };
            frame.Add(header);

            if (farmGroup == null || farmGroup.FarmPlots.Count == 0)
            {
                frame.Dispose();
                return;
            }

            // Calculate dimensions
            float rectHeight = viewport.Y / 8f;
            float rectWidth = rectHeight * 0.75f;
            float spacing = 10f;

            // Calculate how many plots fit per row
            int plotsPerRow = (int)((viewport.X - spacing) / (rectWidth + spacing));
            if (plotsPerRow < 1)
                plotsPerRow = 1;

            // Draw each farm plot
            for (int i = 0; i < farmGroup.FarmPlots.Count; i++)
            {
                var plot = farmGroup.FarmPlots[i];
                int row = i / plotsPerRow;
                int col = i % plotsPerRow;

                // Calculate plots in current row for centering
                int plotsInThisRow =
                    (i / plotsPerRow == farmGroup.FarmPlots.Count / plotsPerRow)
                        ? farmGroup.FarmPlots.Count % plotsPerRow
                        : plotsPerRow;
                if (plotsInThisRow == 0)
                    plotsInThisRow = plotsPerRow;

                // Calculate total width of this row
                float rowWidth = plotsInThisRow * rectWidth + (plotsInThisRow - 1) * spacing;
                float rowStartX = (viewport.X - rowWidth) / 2f;

                // Calculate position
                float x = rowStartX + col * (rectWidth + spacing) + rectWidth / 2f;
                float y = viewport.Y / 2f - (rectHeight / 2f) + row * (rectHeight + spacing * 2f);

                // Get growth progress and determine color
                float growthProgress = 0f;
                Color outlineColor = Color.Green;

                if (farmGroup.ProgrammableBlock != null)
                {
                    if (plot.IsPlantPlanted)
                    {
                        if (plot.IsPlantAlive)
                        {
                            if (plot.IsPlantFullyGrown)
                            {
                                outlineColor = farmGroup.ProgrammableBlock.PlantedReadyColor;
                            }
                            else
                            {
                                outlineColor = farmGroup.ProgrammableBlock.PlantedAliveColor;
                            }

                            var details = plot.GetPlotDetails();
                            if (details != null)
                            {
                                growthProgress = details.GrowthProgress;
                            }
                        }
                        else
                        {
                            outlineColor = farmGroup.ProgrammableBlock.PlantedDeadColor;
                        }
                    }
                    else
                    {
                        outlineColor = farmGroup.ProgrammableBlock.PlanterEmptyColor;
                    }
                }

                // Draw outline rectangle with state-based color
                var outline = new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = new Vector2(x, y),
                    Size = new Vector2(rectWidth, rectHeight),
                    Color = outlineColor,
                    Alignment = TextAlignment.CENTER,
                };
                frame.Add(outline);

                // Draw black background (creates padding inside green border)
                float padding = 4f;
                var background = new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = new Vector2(x, y),
                    Size = new Vector2(rectWidth - padding, rectHeight - padding),
                    Color = surface.ScriptBackgroundColor,
                    Alignment = TextAlignment.CENTER,
                };
                frame.Add(background);

                // Draw filled rectangle based on growth
                if (growthProgress > 0f)
                {
                    float innerPadding = padding + 8f;
                    float availableHeight = rectHeight - innerPadding;
                    float filledHeight = availableHeight * growthProgress;
                    float filledY = y + (rectHeight - filledHeight - innerPadding) / 2f;

                    var filled = new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Position = new Vector2(x, filledY),
                        Size = new Vector2(rectWidth - innerPadding, filledHeight),
                        Color = outlineColor,
                        Alignment = TextAlignment.CENTER,
                    };
                    frame.Add(filled);
                }

                // Draw water level indicator below the farm plot
                float waterRectHeight = rectHeight / 4f;
                float waterRectY = y + (rectHeight / 2f) + waterRectHeight - spacing;

                // Water level outline (same color as farm plot)
                var waterOutline = new MySprite()
                {
                    Type = SpriteType.TEXTURE,
                    Data = "SquareSimple",
                    Position = new Vector2(x, waterRectY),
                    Size = new Vector2(rectWidth, waterRectHeight),
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
                    Size = new Vector2(rectWidth - padding, waterRectHeight - padding),
                    Color = Color.Black,
                    Alignment = TextAlignment.CENTER,
                };
                frame.Add(waterBackground);

                // Water level fill bar (blue, left to right)
                float waterRatio = (float)plot.WaterFilledRatio;
                if (waterRatio > 0f)
                {
                    float waterInnerPadding = padding + 2f;
                    float availableWidth = rectWidth - waterInnerPadding;
                    float filledWidth = availableWidth * waterRatio;
                    float waterFilledX = x - (availableWidth / 2f) + (filledWidth / 2f);

                    var waterFilled = new MySprite()
                    {
                        Type = SpriteType.TEXTURE,
                        Data = "SquareSimple",
                        Position = new Vector2(waterFilledX, waterRectY),
                        Size = new Vector2(filledWidth, waterRectHeight - waterInnerPadding),
                        Color = Color.Blue,
                        Alignment = TextAlignment.CENTER,
                    };
                    frame.Add(waterFilled);
                }
            }

            frame.Dispose();
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
