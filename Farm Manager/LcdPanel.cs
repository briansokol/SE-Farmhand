using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    internal class LcdPanel : Block
    {
        private readonly IMyTextPanel _lcdPanel;

        private readonly Dictionary<string, string> _customDataEntries =
            new Dictionary<string, string>();

        protected override IMyFunctionalBlock BlockInstance => _lcdPanel;
        protected override Dictionary<string, string> CustomDataEntries => _customDataEntries;

        public LcdPanel(IMyTextPanel lcdPanel, MyGridProgram program)
            : base(program)
        {
            _lcdPanel = lcdPanel;
            _lcdPanel.ContentType = ContentType.TEXT_AND_IMAGE;
        }

        public void WriteText(string text, bool append = false)
        {
            if (IsFunctional())
            {
                _lcdPanel.WriteText(text, append);
            }
        }

        public static bool BlockIsValid(IMyTextPanel block)
        {
            return IsValid(block);
        }
    }
}
