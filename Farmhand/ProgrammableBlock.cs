using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    internal class ProgrammableBlock : Block
    {
        private readonly IMyProgrammableBlock _programmableBlock;
        private readonly IMyTextSurface _lcdScreen;

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
                    "Make sure all blocks, including this programmable block, are in the same group"
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
        /// Gets or sets the block group name for farm management
        /// </summary>
        public string GroupName
        {
            get
            {
                ParseCustomData();
                return _customData
                    .Get(_customDataHeader, _customDataConfigs["GroupName"].Label)
                    .ToString("");
            }
        }

        /// <summary>
        /// Writes text to the programmable block's LCD screen
        /// </summary>
        /// <param name="text">Text to display</param>
        /// <param name="append">Whether to append to existing text</param>
        public void WriteToLcd(string text, bool append = false)
        {
            if (IsFunctional() && _lcdScreen != null)
            {
                _program.Echo(text);
                _lcdScreen.WriteText(text, append);
            }
        }
    }
}
