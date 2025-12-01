using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Self-referential block for configuration display on the programmable block's LCD screen
    /// </summary>
    internal class ProgrammableBlock : Block
    {
        private readonly IMyProgrammableBlock _programmableBlock;
        private readonly IMyTextSurface _lcdScreen;
        protected readonly StringBuilder _lcdOutput = new StringBuilder();

        // Cached color values to avoid repeated parsing
        private Color? _cachedPlanterEmptyColor;
        private Color? _cachedPlantedAliveColor;
        private Color? _cachedPlantedReadyColor;
        private Color? _cachedPlantedDeadColor;
        private Color? _cachedWaterLowColor;
        private string _lastCustomData;

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
                    "Use only if you don't want to see status on an LCD"
                )
            },
            {
                "ControlFarmPlotLights",
                new CustomDataConfig(
                    "Control Farm Plot Lights",
                    "true",
                    "Enable automatic farm plot light control (set false if feature is buggy on your server)"
                )
            },
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
                    "255,0,25",
                    "RGB color for dead plants (default: 255,0,25)"
                )
            },
            {
                "WaterLowColor",
                new CustomDataConfig(
                    "Water Low Color",
                    "0,65,255",
                    "RGB color for low water warning (default: 0,65,255)"
                )
            },
            {
                "IceLowThreshold",
                new CustomDataConfig(
                    "Ice Low Threshold",
                    "0.2",
                    "Low ice percent threshold, between 0.0 and 1.0 (default: 0.2)"
                )
            },
            {
                "WaterTankLowThreshold",
                new CustomDataConfig(
                    "Water Tank Low Threshold",
                    "0.2",
                    "Low water tank percent threshold, between 0.0 and 1.0 (default: 0.2)"
                )
            },
            {
                "WaterLowThreshold",
                new CustomDataConfig(
                    "Water Low Threshold",
                    "0.5",
                    "Low water percent threshold, between 0.0 and 1.0 (default: 0.5)"
                )
            },
            {
                "HealthLowThreshold",
                new CustomDataConfig(
                    "Health Low Threshold",
                    "1.0",
                    "Low health percent threshold, between 0.0 and 1.0 (default: 1.0)"
                )
            },
        };

        public override IMyTerminalBlock BlockInstance => _programmableBlock;
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
        /// Gets the group name from custom data
        /// </summary>
        /// <returns>The configured group name</returns>
        public string GroupName()
        {
            ParseCustomData();
            return _customData
                .Get(_customDataHeader, _customDataConfigs["GroupName"].Label)
                .ToString("");
        }

        /// <summary>
        /// Checks if custom data has changed and invalidates color cache if needed
        /// </summary>
        private void CheckAndInvalidateColorCache()
        {
            string currentCustomData = BlockInstance.CustomData;
            if (_lastCustomData != currentCustomData)
            {
                _lastCustomData = currentCustomData;
                _cachedPlanterEmptyColor = null;
                _cachedPlantedAliveColor = null;
                _cachedPlantedReadyColor = null;
                _cachedPlantedDeadColor = null;
                _cachedWaterLowColor = null;
            }
        }

        /// <summary>
        /// Gets the color for empty farm plots (cached)
        /// </summary>
        public Color PlanterEmptyColor
        {
            get
            {
                CheckAndInvalidateColorCache();
                if (_cachedPlanterEmptyColor == null)
                {
                    ParseCustomData();
                    _cachedPlanterEmptyColor = ParseColor(
                        _customData
                            .Get(_customDataHeader, _customDataConfigs["PlanterEmptyColor"].Label)
                            .ToString(_customDataConfigs["PlanterEmptyColor"].DefaultValue)
                    );
                }
                return _cachedPlanterEmptyColor.Value;
            }
        }

        /// <summary>
        /// Gets the color for growing plants (cached)
        /// </summary>
        public Color PlantedAliveColor
        {
            get
            {
                CheckAndInvalidateColorCache();
                if (_cachedPlantedAliveColor == null)
                {
                    ParseCustomData();
                    _cachedPlantedAliveColor = ParseColor(
                        _customData
                            .Get(_customDataHeader, _customDataConfigs["PlantedAliveColor"].Label)
                            .ToString(_customDataConfigs["PlantedAliveColor"].DefaultValue)
                    );
                }
                return _cachedPlantedAliveColor.Value;
            }
        }

        /// <summary>
        /// Gets the color for ready-to-harvest plants (cached)
        /// </summary>
        public Color PlantedReadyColor
        {
            get
            {
                CheckAndInvalidateColorCache();
                if (_cachedPlantedReadyColor == null)
                {
                    ParseCustomData();
                    _cachedPlantedReadyColor = ParseColor(
                        _customData
                            .Get(_customDataHeader, _customDataConfigs["PlantedReadyColor"].Label)
                            .ToString(_customDataConfigs["PlantedReadyColor"].DefaultValue)
                    );
                }
                return _cachedPlantedReadyColor.Value;
            }
        }

        /// <summary>
        /// Gets the color for dead plants (cached)
        /// </summary>
        public Color PlantedDeadColor
        {
            get
            {
                CheckAndInvalidateColorCache();
                if (_cachedPlantedDeadColor == null)
                {
                    ParseCustomData();
                    _cachedPlantedDeadColor = ParseColor(
                        _customData
                            .Get(_customDataHeader, _customDataConfigs["PlantedDeadColor"].Label)
                            .ToString(_customDataConfigs["PlantedDeadColor"].DefaultValue)
                    );
                }
                return _cachedPlantedDeadColor.Value;
            }
        }

        /// <summary>
        /// Gets the color for low water warning (cached)
        /// </summary>
        public Color WaterLowColor
        {
            get
            {
                CheckAndInvalidateColorCache();
                if (_cachedWaterLowColor == null)
                {
                    ParseCustomData();
                    _cachedWaterLowColor = ParseColor(
                        _customData
                            .Get(_customDataHeader, _customDataConfigs["WaterLowColor"].Label)
                            .ToString(_customDataConfigs["WaterLowColor"].DefaultValue)
                    );
                }
                return _cachedWaterLowColor.Value;
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
        /// Gets the water tank low threshold percentage
        /// </summary>
        public float WaterTankLowThreshold
        {
            get
            {
                ParseCustomData();
                var thresholdString = _customData
                    .Get(_customDataHeader, _customDataConfigs["WaterTankLowThreshold"].Label)
                    .ToString(_customDataConfigs["WaterTankLowThreshold"].DefaultValue);

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
        /// Gets the health low threshold percentage
        /// </summary>
        public float HealthLowThreshold
        {
            get
            {
                ParseCustomData();
                var thresholdString = _customData
                    .Get(_customDataHeader, _customDataConfigs["HealthLowThreshold"].Label)
                    .ToString(_customDataConfigs["HealthLowThreshold"].DefaultValue);

                float threshold;
                if (float.TryParse(thresholdString, out threshold))
                {
                    // Clamp between 0.0 and 1.0
                    return Math.Max(0.0f, Math.Min(1.0f, threshold));
                }
                // Return default if parsing fails
                return 1.0f;
            }
        }

        /// <summary>
        /// Gets whether automatic farm plot light control is enabled
        /// </summary>
        public bool ControlFarmPlotLights
        {
            get { return GetCustomDataBool("ControlFarmPlotLights", true); }
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
        /// Appends text to the internal buffer for display on the programmable block's LCD
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="header">Whether to format the text as a header</param>
        public void AppendText(string text, bool header = false)
        {
            if (IsFunctional() && _lcdScreen != null)
            {
                if (header)
                {
                    _lcdOutput.AppendLine(text);
                }
                else
                {
                    _lcdOutput.AppendLine("  " + text);
                }
            }
        }

        /// <summary>
        /// Flushes the accumulated text to the programmable block's LCD and clears the buffer
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
