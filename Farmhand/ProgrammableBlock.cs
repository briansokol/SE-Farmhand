using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    internal class ProgrammableBlock : Block
    {
        private readonly IMyProgrammableBlock _programmableBlock;
        private readonly IMyTextSurface _lcdScreen;
        protected readonly StringBuilder _lcdOutput = new StringBuilder();

        private readonly Dictionary<string, CustomDataConfig> _customDataConfigs = new Dictionary<
            string,
            CustomDataConfig
        >()
        {
            {
                "GroupName",
                new CustomDataConfig(
                    "Group Name",
                    "",
                    "Make sure all blocks you want to track are in the same group"
                )
            },
            {
                "PlanterEmptyColor",
                new CustomDataConfig(
                    "Plot Empty Color",
                    "80,0,170",
                    "RGB color for empty farm plots (default: purple)"
                )
            },
            {
                "PlantedAliveColor",
                new CustomDataConfig(
                    "Plant Alive Color",
                    "255,255,255",
                    "RGB color for growing plants (default: white)"
                )
            },
            {
                "PlantedReadyColor",
                new CustomDataConfig(
                    "Plant Ready Color",
                    "0,255,185",
                    "RGB color for ready-to-harvest plants (default: green)"
                )
            },
            {
                "PlantedDeadColor",
                new CustomDataConfig(
                    "Plant Dead Color",
                    "255,0,100",
                    "RGB color for dead plants (default: red)"
                )
            },
        };

        protected override IMyFunctionalBlock BlockInstance => _programmableBlock;
        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs =>
            _customDataConfigs;

        /// <summary>
        /// Initializes a new instance of the ProgrammableBlock class
        /// </summary>
        /// <param name="programmableBlock">The Space Engineers programmable block to wrap</param>
        /// <param name="program">The parent grid program instance</param>
        public ProgrammableBlock(IMyProgrammableBlock programmableBlock, MyGridProgram program)
            : base(program)
        {
            _programmableBlock = programmableBlock;
            _lcdScreen = _programmableBlock.GetSurface(0);
            _lcdScreen.ContentType = ContentType.TEXT_AND_IMAGE;
            UpdateCustomData();
        }

        /// <summary>
        /// Gets or sets the block group name for farm management
        /// </summary>
        public string GroupName
        {
            get
            {
                ParseCustomData();
                return _customData
                    .Get(_customDataHeader, _customDataConfigs["GroupName"].Label)
                    .ToString("");
            }
        }

        /// <summary>
        /// Gets the color for empty farm plots
        /// </summary>
        public Color PlanterEmptyColor
        {
            get
            {
                ParseCustomData();
                return ParseColor(
                    _customData
                        .Get(_customDataHeader, _customDataConfigs["PlanterEmptyColor"].Label)
                        .ToString(_customDataConfigs["PlanterEmptyColor"].DefaultValue)
                );
            }
        }

        /// <summary>
        /// Gets the color for growing plants
        /// </summary>
        public Color PlantedAliveColor
        {
            get
            {
                ParseCustomData();
                return ParseColor(
                    _customData
                        .Get(_customDataHeader, _customDataConfigs["PlantedAliveColor"].Label)
                        .ToString(_customDataConfigs["PlantedAliveColor"].DefaultValue)
                );
            }
        }

        /// <summary>
        /// Gets the color for ready-to-harvest plants
        /// </summary>
        public Color PlantedReadyColor
        {
            get
            {
                ParseCustomData();
                return ParseColor(
                    _customData
                        .Get(_customDataHeader, _customDataConfigs["PlantedReadyColor"].Label)
                        .ToString(_customDataConfigs["PlantedReadyColor"].DefaultValue)
                );
            }
        }

        /// <summary>
        /// Gets the color for dead plants
        /// </summary>
        public Color PlantedDeadColor
        {
            get
            {
                ParseCustomData();
                return ParseColor(
                    _customData
                        .Get(_customDataHeader, _customDataConfigs["PlantedDeadColor"].Label)
                        .ToString(_customDataConfigs["PlantedDeadColor"].DefaultValue)
                );
            }
        }

        /// <summary>
        /// Parses a color string in the format "R,G,B" into a Color object
        /// </summary>
        /// <param name="colorString">RGB color string (e.g., "255,0,100")</param>
        /// <returns>Color object</returns>
        private Color ParseColor(string colorString)
        {
            var parts = colorString.Split(',');
            if (parts.Length == 3)
            {
                int r,
                    g,
                    b;
                if (
                    int.TryParse(parts[0].Trim(), out r)
                    && int.TryParse(parts[1].Trim(), out g)
                    && int.TryParse(parts[2].Trim(), out b)
                )
                {
                    return new Color(r, g, b);
                }
            }
            // Return default black color if parsing fails
            return new Color(0, 0, 0);
        }

        /// <summary>
        /// Writes text to the terminal and appends it to the internal buffer
        /// </summary>
        /// <param name="text">Text to display</param>
        public void AppendText(string text)
        {
            if (IsFunctional() && _lcdScreen != null)
            {
                _program.Echo(text);
                _lcdOutput.AppendLine(text);
            }
        }

        /// <summary>
        /// Flushes the accumulated text to the LCD panel and clears the buffer
        /// </summary>
        public void FlushTextToScreen()
        {
            if (IsFunctional() && _lcdScreen != null)
            {
                _lcdScreen.WriteText(_lcdOutput.ToString(), false);
                _lcdOutput.Clear();
            }
        }
    }
}
