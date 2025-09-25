using System;
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
                "PlanterEmptyColor",
                new CustomDataConfig(
                    "Plot Empty Color",
                    "80,0,170",
                    "RGB color for empty farm plots (default: 80,0,170)"
                )
            },
            {
                "PlantedAliveColor",
                new CustomDataConfig(
                    "Plant Alive Color",
                    "255,255,255",
                    "RGB color for growing plants (default: 255,255,255)"
                )
            },
            {
                "PlantedReadyColor",
                new CustomDataConfig(
                    "Plant Ready Color",
                    "0,255,185",
                    "RGB color for ready-to-harvest plants (default: 0,255,185)"
                )
            },
            {
                "PlantedDeadColor",
                new CustomDataConfig(
                    "Plant Dead Color",
                    "255,0,100",
                    "RGB color for dead plants (default: 255,0,100)"
                )
            },
            {
                "IceLowThreshold",
                new CustomDataConfig(
                    "Ice Low Threshold",
                    "0.2",
                    "Ice level threshold (0.0-1.0) that triggers low ice alerts and events (default: 0.2 = 20%)"
                )
            },
            {
                "WaterLowThreshold",
                new CustomDataConfig(
                    "Water Low Threshold",
                    "0.2",
                    "Water level threshold (0.0-1.0) that triggers low water alerts and blinking lights (default: 0.2 = 20%)"
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
        /// Gets the ice low threshold percentage
        /// </summary>
        public float IceLowThreshold
        {
            get
            {
                ParseCustomData();
                var thresholdString = _customData
                    .Get(_customDataHeader, _customDataConfigs["IceLowThreshold"].Label)
                    .ToString(_customDataConfigs["IceLowThreshold"].DefaultValue);

                float threshold;
                if (float.TryParse(thresholdString, out threshold))
                {
                    // Clamp between 0.0 and 1.0
                    return Math.Max(0.0f, Math.Min(1.0f, threshold));
                }
                // Return default if parsing fails
                return 0.2f;
            }
        }

        /// <summary>
        /// Gets the water low threshold percentage
        /// </summary>
        public float WaterLowThreshold
        {
            get
            {
                ParseCustomData();
                var thresholdString = _customData
                    .Get(_customDataHeader, _customDataConfigs["WaterLowThreshold"].Label)
                    .ToString(_customDataConfigs["WaterLowThreshold"].DefaultValue);

                float threshold;
                if (float.TryParse(thresholdString, out threshold))
                {
                    // Clamp between 0.0 and 1.0
                    return Math.Max(0.0f, Math.Min(1.0f, threshold));
                }
                // Return default if parsing fails
                return 0.2f;
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
