using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    /// <summary>
    /// Controls single-screen LCD text display panels for farm information output
    /// </summary>
    internal class LcdPanel : Block
    {
        private readonly IMyTextPanel _lcdPanel;
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
                "Header",
                new CustomDataConfig("Header", "true", "Shows the animated header on the screen")
            },
            {
                "ShowAlerts",
                new CustomDataConfig("Show Alerts", "true", "Shows information requiring attention")
            },
            {
                "ShowFarmPlots",
                new CustomDataConfig("Show Farm Plots", "true", "Shows farm plot information")
            },
            {
                "ShowAtmosphere",
                new CustomDataConfig("Show Atmosphere", "true", "Shows atmospheric information")
            },
            {
                "ShowIrrigation",
                new CustomDataConfig("Show Irrigation", "true", "Shows irrigation system status")
            },
            { "ShowYield", new CustomDataConfig("Show Yield", "true", "Shows current crop yield") },
            {
                "TextAlignment",
                new CustomDataConfig(
                    "Text Alignment",
                    "Left",
                    "Text alignment on screen (Left or Center)"
                )
            },
        };

        public override IMyTerminalBlock BlockInstance => _lcdPanel;
        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs =>
            _customDataConfigs;

        /// <summary>
        /// Initializes a new instance of the LcdPanel class
        /// </summary>
        /// <param name="lcdPanel">The Space Engineers text panel block to wrap</param>
        /// <param name="program">The parent grid program instance</param>
        public LcdPanel(IMyTextPanel lcdPanel, MyGridProgram program)
            : base(program)
        {
            _lcdPanel = lcdPanel;
            UpdateCustomData();
        }

        /// <summary>
        /// Gets the group name from custom data
        /// </summary>
        /// <returns></returns>
        public string GroupName()
        {
            ParseCustomData();
            return _customData
                .Get(_customDataHeader, _customDataConfigs["GroupName"].Label)
                .ToString("");
        }

        /// <summary>
        /// Writes text to the internal buffer if the category is set to be visible
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="category">The category of the text</param>
        /// <param name="isHeader">Whether this text is a header (headers are not indented)</param>
        public void AppendText(string text, string category = null, bool isHeader = false)
        {
            if (IsFunctional() && _lcdPanel != null)
            {
                if (category == null || IsCategoryVisible(category))
                {
                    // Add 2-space indentation for non-headers when left-aligned
                    string outputText = text;
                    if (!isHeader && GetTextAlignment() == TextAlignment.LEFT)
                    {
                        outputText = "  " + text;
                    }
                    _lcdOutput.AppendLine(outputText);
                }
            }
        }

        /// <summary>
        /// Flushes the accumulated text to the LCD panel and clears the buffer
        /// </summary>
        public void FlushTextToScreen()
        {
            if (IsFunctional() && _lcdPanel != null)
            {
                _lcdPanel.ContentType = ContentType.TEXT_AND_IMAGE;
                _lcdPanel.Alignment = GetTextAlignment();
                _lcdPanel.WriteText(_lcdOutput.ToString(), false);
                _lcdOutput.Clear();
            }
        }

        /// <summary>
        /// Gets the configured text alignment from custom data
        /// </summary>
        /// <returns>The text alignment setting (defaults to LEFT)</returns>
        private TextAlignment GetTextAlignment()
        {
            try
            {
                ParseCustomData();
                string alignment = _customData
                    .Get(_customDataHeader, _customDataConfigs["TextAlignment"].Label)
                    .ToString("Left");

                if (alignment.Equals("Center", System.StringComparison.OrdinalIgnoreCase))
                {
                    return TextAlignment.CENTER;
                }

                return TextAlignment.LEFT;
            }
            catch
            {
                return TextAlignment.LEFT;
            }
        }

        /// <summary>
        /// Checks if a specific category is set to be visible on the LCD panel
        /// </summary>
        /// <param name="category">The category to check</param>
        /// <returns>True if the category is set to be visible</returns>
        public bool IsCategoryVisible(string category)
        {
            try
            {
                ParseCustomData();
                return _customData
                    .Get(_customDataHeader, _customDataConfigs[category].Label)
                    .ToBoolean(false);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates whether a block can be used as an LCD panel
        /// </summary>
        /// <param name="block">The text panel block to validate</param>
        /// <returns>True if the block can be used as an LCD panel</returns>
        public static bool BlockIsValid(IMyTerminalBlock block)
        {
            return block is IMyTextPanel && IsBlockValid(block) && (block as IMyTextPanel).Enabled;
        }
    }
}
