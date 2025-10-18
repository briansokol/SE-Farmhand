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
                "Title",
                new CustomDataConfig(
                    "Title",
                    "",
                    "Optional custom title text displayed below the header"
                )
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
        /// Gets the custom title text from custom data
        /// </summary>
        /// <returns>The title text, or empty string if not set</returns>
        private string GetTitle()
        {
            ParseCustomData();
            return _customData
                .Get(_customDataHeader, _customDataConfigs["Title"].Label)
                .ToString("");
        }

        /// <summary>
        /// Writes text to the internal buffer if the category is set to be visible
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="category">The category of the text</param>
        /// <param name="isHeader">Whether this text is a header (headers are not indented)</param>
        /// <param name="runNumber">Animation frame number for animated header (0-2)</param>
        public void AppendText(
            string text,
            string category = null,
            bool isHeader = false,
            int runNumber = 0
        )
        {
            if (IsFunctional() && _lcdPanel != null)
            {
                if (category == null || IsCategoryVisible(category))
                {
                    string outputText = text;

                    // Handle Title category - get title from custom data
                    if (category == "Title")
                    {
                        outputText = GetTitle();
                        // If title is empty, don't write anything (not even a blank line)
                        if (string.IsNullOrEmpty(outputText))
                        {
                            return;
                        }
                    }
                    // Apply header animation if this is the animated header
                    else if (category == "Header" && isHeader && !string.IsNullOrEmpty(text))
                    {
                        var customTitle = GetTitle();
                        var headerTitle = string.IsNullOrEmpty(customTitle) ? text : customTitle;
                        outputText = GetHeaderAnimation(runNumber, headerTitle);
                    }
                    // Add 2-space indentation for non-headers when left-aligned
                    else if (!isHeader && GetTextAlignment() == TextAlignment.LEFT)
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
        /// Generates the animated header text based on text alignment and animation frame
        /// </summary>
        /// <param name="runNumber">Animation frame number (0-2)</param>
        /// <param name="title">The title text to display in the header (defaults to "Farmhand")</param>
        /// <returns>Formatted header string with animation</returns>
        private string GetHeaderAnimation(int runNumber, string title = "Farmhand")
        {
            var alignment = GetTextAlignment();

            if (alignment == TextAlignment.CENTER)
            {
                // Center alignment: animated dots/dashes on both sides
                switch (runNumber)
                {
                    case 0:
                        return $"––• {title} •––";
                    case 1:
                        return $"–•– {title} –•–";
                    case 2:
                        return $"•–– {title} ––•";
                    default:
                        return title;
                }
            }
            else
            {
                // Left alignment: animated dots/dashes on right side only
                switch (runNumber)
                {
                    case 0:
                        return $"{title} •––";
                    case 1:
                        return $"{title} –•–";
                    case 2:
                        return $"{title} ––•";
                    default:
                        return title;
                }
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
