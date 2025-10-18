using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    /// <summary>
    /// Controls cockpit displays with multiple screens for categorized farm information output
    /// </summary>
    internal class Cockpit : Block
    {
        private readonly IMyCockpit _cockpit;
        protected readonly List<StringBuilder> _lcdOutput = new List<StringBuilder>();

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
                new CustomDataConfig("Header", "true", "Shows the animated header on each screen")
            },
            {
                "ShowAlerts",
                new CustomDataConfig(
                    "Show Alerts",
                    "0",
                    "Shows information requiring attention (set index of screen, false to hide)"
                )
            },
            {
                "ShowAtmosphere",
                new CustomDataConfig(
                    "Show Atmosphere",
                    "0",
                    "Shows atmospheric information (set index of screen, false to hide)"
                )
            },
            {
                "ShowIrrigation",
                new CustomDataConfig(
                    "Show Irrigation",
                    "0",
                    "Shows irrigation system status (set index of screen, false to hide)"
                )
            },
            {
                "ShowYield",
                new CustomDataConfig(
                    "Show Yield",
                    "0",
                    "Shows current crop yield (set index of screen, false to hide)"
                )
            },
            {
                "TextAlignment",
                new CustomDataConfig(
                    "Text Alignment",
                    "Left",
                    "Text alignment on screen (Left or Center)"
                )
            },
        };

        public override IMyTerminalBlock BlockInstance => _cockpit;
        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs =>
            _customDataConfigs;

        /// <summary>
        /// Initializes a new instance of the Cockpit class
        /// </summary>
        /// <param name="cockpit">The Space Engineers cockpit block to wrap</param>
        /// <param name="program">The parent grid program instance</param>
        public Cockpit(IMyCockpit cockpit, MyGridProgram program)
            : base(program)
        {
            _cockpit = cockpit;
            for (int i = 0; i < _cockpit.SurfaceCount; i++)
            {
                _lcdOutput.Add(new StringBuilder());
            }
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
            if (IsFunctional() && _cockpit != null)
            {
                // Add 2-space indentation for non-headers when left-aligned
                string outputText = text;
                if (!isHeader && GetTextAlignment() == TextAlignment.LEFT)
                {
                    outputText = "  " + text;
                }

                if (category == null || (category == "Header" && ShouldShowHeader()))
                {
                    var activeScreens = GetActiveScreens();
                    activeScreens.ForEach(index =>
                    {
                        _lcdOutput[index].AppendLine(outputText);
                    });
                }
                else
                {
                    var screenIndex = GetScreenForCategory(category);
                    if (screenIndex >= 0)
                    {
                        _lcdOutput[screenIndex].AppendLine(outputText);
                    }
                }
            }
        }

        /// <summary>
        /// Flushes the accumulated text to the LCD panels and clears the buffer
        /// </summary>
        public void FlushTextToScreens()
        {
            if (IsFunctional() && _cockpit != null)
            {
                foreach (var index in GetActiveScreens())
                {
                    var screen = _cockpit.GetSurface(index);
                    screen.ContentType = ContentType.TEXT_AND_IMAGE;
                    screen.Alignment = GetTextAlignment();
                    screen.WriteText(_lcdOutput[index].ToString(), false);
                    _lcdOutput[index].Clear();
                }
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

        public int GetScreenForCategory(string category)
        {
            try
            {
                ParseCustomData();
                string configValue = _customData
                    .Get(_customDataHeader, _customDataConfigs[category].Label)
                    .ToString("false");

                if (configValue != "false")
                {
                    int screenIndex;
                    if (int.TryParse(configValue, out screenIndex) && screenIndex >= 0)
                    {
                        return screenIndex;
                    }
                }
            }
            catch
            {
                // Ignore errors and fall through to return -1
            }
            return -1;
        }

        public List<int> GetActiveScreens()
        {
            ParseCustomData();
            List<int> activeScreens = new List<int>();

            foreach (var entry in _customDataConfigs)
            {
                if (entry.Key.StartsWith("Show"))
                {
                    var screenIndex = GetScreenForCategory(entry.Key);
                    if (screenIndex >= 0 && !activeScreens.Contains(screenIndex))
                    {
                        activeScreens.Add(screenIndex);
                    }
                }
            }
            return activeScreens;
        }

        /// <summary>
        /// Checks if the header should be shown based on custom data configuration
        /// </summary>
        /// <returns>True if the header should be displayed</returns>
        private bool ShouldShowHeader()
        {
            ParseCustomData();
            return _customData
                .Get(_customDataHeader, _customDataConfigs["Header"].Label)
                .ToBoolean(false);
        }

        /// <summary>
        /// Validates whether a block can be used as a cockpit
        /// </summary>
        /// <param name="block">The cockpit block to validate</param>
        /// <returns>True if the block can be used as a cockpit</returns>
        public static bool BlockIsValid(IMyTerminalBlock block)
        {
            return block is IMyCockpit
                && (block as IMyCockpit).SurfaceCount > 0
                && IsBlockValid(block);
        }
    }
}
