using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Controls individual farm plot LCD displays (512x73 resolution) that show status for a single nearby plot
    /// </summary>
    internal class PlotLCD : Block
    {
        private readonly IMyTextPanel _lcdPanel;
        private FarmPlot _leftFarmPlot;
        private FarmPlot _rightFarmPlot;
        private bool _isCorrectResolution;
        private readonly MyGridProgram _gridProgram;

        // Layout constants
        private const int PADDING = 3;
        private const int WATER_BAR_WIDTH = 30;
        private const float EXPECTED_WIDTH = 512f;
        private const float EXPECTED_HEIGHT = 73f;

        public override IMyTerminalBlock BlockInstance => _lcdPanel;
        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs => null;

        // Used to force redraw of sprites on server clients
        private readonly bool _shiftSprites;

        /// <summary>
        /// Gets whether this LCD has the correct resolution (512x73)
        /// </summary>
        public bool IsCorrectResolution => _isCorrectResolution;

        /// <summary>
        /// Gets the nearby farm plot reference (null if none found)
        /// Returns left plot for backwards compatibility
        /// </summary>
        public FarmPlot NearbyFarmPlot => _leftFarmPlot;

        /// <summary>
        /// Gets the left farm plot reference (null if none found)
        /// </summary>
        public FarmPlot LeftFarmPlot => _leftFarmPlot;

        /// <summary>
        /// Gets the right farm plot reference (null if none found)
        /// </summary>
        public FarmPlot RightFarmPlot => _rightFarmPlot;

        /// <summary>
        /// Initializes a new instance of the PlotLCD class
        /// </summary>
        /// <param name="lcdPanel">The Space Engineers text panel block to wrap</param>
        /// <param name="program">The parent grid program instance</param>
        /// <param name="shiftSprites">Whether to shift sprites for redraw on server clients</param>
        public PlotLCD(IMyTextPanel lcdPanel, MyGridProgram program, bool shiftSprites)
            : base(program)
        {
            _lcdPanel = lcdPanel;
            _gridProgram = program;
            _shiftSprites = shiftSprites;
            CheckResolution();
        }

        /// <summary>
        /// Checks if the LCD panel has the correct resolution (512x73)
        /// </summary>
        private void CheckResolution()
        {
            if (_lcdPanel != null)
            {
                // Round surface size to nearest integer before comparing
                int actualWidth = (int)System.Math.Round(_lcdPanel.SurfaceSize.X);
                int actualHeight = (int)System.Math.Round(_lcdPanel.SurfaceSize.Y);

                _isCorrectResolution = (
                    actualWidth == EXPECTED_WIDTH && actualHeight == EXPECTED_HEIGHT
                );
            }
            else
            {
                _isCorrectResolution = false;
            }
        }

        /// <summary>
        /// Attempts to find nearby farm plots on the left and right sides, or falls back to behind/above/below
        /// </summary>
        public void FindNearbyFarmPlot()
        {
            // Reset previous references
            _leftFarmPlot = null;
            _rightFarmPlot = null;

            // Only search if resolution is correct
            if (!_isCorrectResolution || !IsFunctional())
            {
                return;
            }

            var grid = _lcdPanel.CubeGrid;
            var lcdPosition = _lcdPanel.Position;
            var orientation = _lcdPanel.Orientation;

            // The corner LCDs are essentially laying on their face,
            // So we need to adjust our search directions accordingly.
            // Their down is forward and their forward is up.

            // Get left and right vectors
            // Using left because the LCDs are rotated
            Base6Directions.Direction leftDir = orientation.Left;
            Vector3I leftOffset = Base6Directions.GetIntVector(leftDir);
            Vector3I rightOffset = -leftOffset;

            // Check left position
            var leftPos = lcdPosition + leftOffset;
            var leftBlock = grid.GetCubeBlock(leftPos);
            if (leftBlock != null && leftBlock.FatBlock != null)
            {
                var terminalBlock = leftBlock.FatBlock as IMyTerminalBlock;
                if (
                    terminalBlock != null
                    && FarmPlot.BlockIsValid(terminalBlock as IMyFunctionalBlock)
                )
                {
                    _leftFarmPlot = new FarmPlot(terminalBlock as IMyFunctionalBlock, _program);
                }
            }

            // Check right position
            var rightPos = lcdPosition + rightOffset;
            var rightBlock = grid.GetCubeBlock(rightPos);
            if (rightBlock != null && rightBlock.FatBlock != null)
            {
                var terminalBlock = rightBlock.FatBlock as IMyTerminalBlock;
                if (
                    terminalBlock != null
                    && FarmPlot.BlockIsValid(terminalBlock as IMyFunctionalBlock)
                )
                {
                    _rightFarmPlot = new FarmPlot(terminalBlock as IMyFunctionalBlock, _program);
                }
            }

            // If no left/right plots found, fall back to behind/above/below search
            if (_leftFarmPlot == null && _rightFarmPlot == null)
            {
                // Get the forward vector (behind the LCD)
                // Using up because the LCDs are rotated such that up is their forward
                Base6Directions.Direction forwardDir = orientation.Up;
                Vector3I forwardOffset = -1 * Base6Directions.GetIntVector(forwardDir);

                // Get the up and down vectors
                // Using forward because the LCDs are rotated such that forward is "down" on the screen
                Base6Directions.Direction downDir = orientation.Forward;
                Vector3I downOffset = Base6Directions.GetIntVector(downDir);
                Vector3I upOffset = -downOffset;

                // Check three positions: behind, above, below
                Vector3I[] searchPositions = new Vector3I[]
                {
                    lcdPosition + forwardOffset, // Behind
                    lcdPosition + upOffset, // Above
                    lcdPosition + downOffset, // Below
                };

                foreach (var searchPos in searchPositions)
                {
                    var block = grid.GetCubeBlock(searchPos);
                    if (block != null && block.FatBlock != null)
                    {
                        var terminalBlock = block.FatBlock as IMyTerminalBlock;
                        if (
                            terminalBlock != null
                            && FarmPlot.BlockIsValid(terminalBlock as IMyFunctionalBlock)
                        )
                        {
                            _leftFarmPlot = new FarmPlot(
                                terminalBlock as IMyFunctionalBlock,
                                _program
                            );
                            return; // Found one, stop searching
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Renders the appropriate display based on resolution and farm plot availability
        /// </summary>
        /// <param name="runNumber">Animation frame number for blinking effects (0-5)</param>
        /// <param name="programmableBlock">Reference to the programmable block for color configuration</param>
        public void Render(int runNumber, ProgrammableBlock programmableBlock)
        {
            if (!IsFunctional() || _lcdPanel == null)
            {
                return;
            }

            // Set content type to SCRIPT for sprite rendering
            _lcdPanel.ContentType = VRage.Game.GUI.TextPanel.ContentType.SCRIPT;

            if (!_isCorrectResolution)
            {
                // Display "Screen Size Unsupported" message in text mode
                _lcdPanel.ContentType = VRage.Game.GUI.TextPanel.ContentType.TEXT_AND_IMAGE;
                _lcdPanel.Alignment = VRage.Game.GUI.TextPanel.TextAlignment.CENTER;
                _lcdPanel.WriteText("Screen Size Unsupported\n\nExpected: 512 x 73", false);
            }
            else
            {
                // Use sprite renderer for correct resolution displays
                var renderer = new PlotLCDRenderer(
                    _lcdPanel,
                    _leftFarmPlot,
                    _rightFarmPlot,
                    runNumber,
                    programmableBlock,
                    _shiftSprites
                );
                renderer.DrawPlotStatus();
            }
        }

        /// <summary>
        /// Validates whether a block can be used as a PlotLCD
        /// </summary>
        /// <param name="block">The text panel block to validate</param>
        /// <returns>True if the block can be used as a PlotLCD (has [PlotLCD] tag and is functional)</returns>
        public static bool BlockIsValid(IMyTerminalBlock block)
        {
            if (!(block is IMyTextPanel) || !IsBlockValid(block))
            {
                return false;
            }

            var textPanel = block as IMyTextPanel;
            if (!textPanel.Enabled)
            {
                return false;
            }

            // Check if custom name contains [PlotLCD] tag
            return block.CustomName.Contains("[PlotLCD]");
        }
    }
}
