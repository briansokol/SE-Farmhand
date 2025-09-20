using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    internal class LcdPanel : Block
    {
        private readonly IMyTextPanel _lcdPanel;
        protected readonly StringBuilder _lcdOutput = new StringBuilder();

        private readonly Dictionary<string, CustomDataConfig> _customDataConfigs = new Dictionary<
            string,
            CustomDataConfig
        >()
        {
            { "ShowAtmosphere", new CustomDataConfig("Show Atmosphere", "true") },
            { "ShowIrrigation", new CustomDataConfig("Show Irrigation", "true") },
            { "ShowYield", new CustomDataConfig("Show Yield", "true") },
            { "ShowErrors", new CustomDataConfig("Show Errors", "true") },
        };

        protected override IMyFunctionalBlock BlockInstance => _lcdPanel;
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
            _lcdPanel.ContentType = ContentType.TEXT_AND_IMAGE;
            UpdateCustomData();
        }

        /// <summary>
        /// Writes text to the internal buffer if the category is set to be visible
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="category">The category of the text</param>
        public void AppendText(string text, string category = null)
        {
            if (IsFunctional() && _lcdPanel != null)
            {
                if (category == null || IsCategoryVisible(category))
                {
                    _lcdOutput.AppendLine(text);
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
                _lcdPanel.WriteText(_lcdOutput.ToString(), false);
                _lcdOutput.Clear();
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
        public static bool BlockIsValid(IMyTextPanel block)
        {
            return IsBlockValid(block);
        }
    }
}
