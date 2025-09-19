using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    internal class ProgrammableBlock : Block
    {
        private readonly IMyProgrammableBlock _programmableBlock;
        private readonly IMyTextSurface _lcdScreen;

        private readonly Dictionary<string, string> _customDataEntries = new Dictionary<
            string,
            string
        >()
        {
            { "GroupName", "Group Name" },
        };

        protected override IMyFunctionalBlock BlockInstance => _programmableBlock;
        protected override Dictionary<string, string> CustomDataEntries => _customDataEntries;

        public ProgrammableBlock(IMyProgrammableBlock programmableBlock, MyGridProgram program)
            : base(program)
        {
            _programmableBlock = programmableBlock;
            _lcdScreen = _programmableBlock.GetSurface(0);
            _lcdScreen.ContentType = ContentType.TEXT_AND_IMAGE;
            UpdateCustomData();
        }

        public string GroupName
        {
            get
            {
                ParseCustomData();
                return _customData
                    .Get(_customDataHeader, _customDataEntries["GroupName"])
                    .ToString("");
            }
            set
            {
                _customData.Set(_customDataHeader, _customDataEntries["GroupName"], value);
                UpdateCustomData();
            }
        }

        public void WriteToLcd(string text, bool append = false)
        {
            if (IsFunctional())
            {
                _program.Echo(text);
                _lcdScreen.WriteText(text, append);
            }
        }
    }
}
