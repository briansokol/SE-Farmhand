using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    internal class LcdPanel : Block
    {
        private readonly IMyTextPanel _lcdPanel;

        protected override IMyFunctionalBlock BlockInstance => _lcdPanel;

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
        /// Writes text to the LCD panel
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="append">Whether to append to existing text</param>
        public void WriteText(string text, bool append = false)
        {
            if (IsFunctional() && _lcdPanel != null)
            {
                _lcdPanel.WriteText(text, append);
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
