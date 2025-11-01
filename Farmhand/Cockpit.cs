using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Controls cockpit displays with multiple screens for categorized farm information output
    /// </summary>
    internal class Cockpit : Block
    {
        private readonly IMyCockpit _cockpit;
        protected readonly List<StringBuilder> _lcdOutput = new List<StringBuilder>();
        private FarmGroup _farmGroup;

        // Used to force redraw of sprites on server clients
        private readonly bool _shiftSprites;

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
                "Title",
                new CustomDataConfig(
                    "Title",
                    "",
                    "Optional custom title text displayed below the header"
                )
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
                    "left",
                    "Text alignment on screen (left or center)"
                )
            },
            {
                "GraphicalMode",
                new CustomDataConfig(
                    "Graphical Mode",
                    "false",
                    "Shows graphical UI instead of text (set index of screen, false to disable)"
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
        /// <param name="shiftSprites">Whether to shift sprites for redraw on server clients</param>
        public Cockpit(IMyCockpit cockpit, MyGridProgram program, bool shiftSprites)
            : base(program)
        {
            _cockpit = cockpit;
            for (int i = 0; i < _cockpit.SurfaceCount; i++)
            {
                _lcdOutput.Add(new StringBuilder());
                _shiftSprites = shiftSprites;
            }
            UpdateCustomData();
        }

        /// <summary>
        /// Gets the group name from custom data
        /// </summary>
        /// <returns>The configured group name</returns>
        public string GroupName()
        {
            return GetCustomDataString("GroupName", "");
        }

        /// <summary>
        /// Gets the custom title text from custom data
        /// </summary>
        /// <returns>The title text, or empty string if not set</returns>
        private string GetTitle()
        {
            return GetCustomDataString("Title", "");
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
            if (IsFunctional() && _cockpit != null)
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
                    outputText = RenderHelpers.GetHeaderAnimation(
                        runNumber,
                        headerTitle,
                        GetTextAlignment()
                    );
                }
                // Add 2-space indentation for non-headers when left-aligned
                else if (!isHeader && GetTextAlignment() == TextAlignment.LEFT)
                {
                    outputText = "  " + text;
                }

                if (
                    category == null
                    || (category == "Header" && ShouldShowHeader())
                    || category == "Title"
                )
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
        /// Gets the screen index for graphical mode display
        /// </summary>
        /// <returns>Screen index or -1 if graphical mode is disabled</returns>
        public int GetGraphicalModeScreen()
        {
            try
            {
                ParseCustomData();
                string configValue = _customData
                    .Get(_customDataHeader, _customDataConfigs["GraphicalMode"].Label)
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

        /// <summary>
        /// Flushes the accumulated text to the LCD panels and clears the buffer
        /// </summary>
        public void FlushTextToScreens()
        {
            if (IsFunctional() && _cockpit != null)
            {
                int graphicalScreen = GetGraphicalModeScreen();

                // Handle graphical mode screen
                if (graphicalScreen >= 0 && graphicalScreen < _cockpit.SurfaceCount)
                {
                    DrawGraphicalUI(graphicalScreen);
                    _lcdOutput[graphicalScreen].Clear();
                }

                // Handle text mode screens
                foreach (var index in GetActiveScreens())
                {
                    if (index != graphicalScreen)
                    {
                        var screen = _cockpit.GetSurface(index);
                        screen.ContentType = ContentType.TEXT_AND_IMAGE;
                        screen.Alignment = GetTextAlignment();
                        screen.WriteText(_lcdOutput[index].ToString(), false);
                        _lcdOutput[index].Clear();
                    }
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
                    .ToString("left");

                if (alignment.Equals("center", System.StringComparison.OrdinalIgnoreCase))
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
        /// Sets the farm group for this cockpit (used to retrieve stats for graphical rendering)
        /// </summary>
        /// <param name="farmGroup">The farm group this cockpit belongs to</param>
        public void SetFarmGroup(FarmGroup farmGroup)
        {
            _farmGroup = farmGroup;
        }

        /// <summary>
        /// Draws the graphical UI using sprites on the specified screen
        /// </summary>
        /// <param name="screenIndex">Index of the screen to draw on</param>
        private void DrawGraphicalUI(int screenIndex)
        {
            var screen = _cockpit.GetSurface(screenIndex);
            var renderer = new SpriteRenderer(screen, _farmGroup, GetTitle(), _shiftSprites);
            renderer.DrawGraphicalUI();
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
            int graphicalScreen = GetGraphicalModeScreen();

            foreach (var entry in _customDataConfigs)
            {
                if (entry.Key.StartsWith("Show"))
                {
                    var screenIndex = GetScreenForCategory(entry.Key);
                    // Exclude graphical screen from text category screens
                    if (
                        screenIndex >= 0
                        && screenIndex != graphicalScreen
                        && !activeScreens.Contains(screenIndex)
                    )
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
            return GetCustomDataBool("Header", false);
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
