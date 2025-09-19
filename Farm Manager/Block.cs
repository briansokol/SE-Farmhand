using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    internal abstract class Block
    {
        protected readonly MyIni _customData = new MyIni();
        protected readonly string _customDataHeader = "Farm Manager";
        protected readonly MyGridProgram _program;

        // Abstract properties that must be implemented by derived classes
        protected abstract IMyFunctionalBlock BlockInstance { get; }
        protected abstract Dictionary<string, string> CustomDataEntries { get; }

        protected Block(MyGridProgram _program)
        {
            this._program = _program;
        }

        public bool IsFunctional()
        {
            return BlockInstance != null && !BlockInstance.Closed && BlockInstance.Enabled;
        }

        protected static bool IsValid(IMyFunctionalBlock block)
        {
            return block != null && !block.Closed && block.Enabled;
        }

        protected void UpdateCustomData()
        {
            if (CustomDataEntries != null && CustomDataEntries.Count > 0)
            {
                ParseCustomData();
                foreach (KeyValuePair<string, string> entry in CustomDataEntries)
                {
                    _customData.Set(
                        _customDataHeader,
                        entry.Value,
                        _customData.Get(_customDataHeader, entry.Value).ToString()
                    );
                }
                BlockInstance.CustomData = _customData.ToString();
            }
        }

        public void ParseCustomData()
        {
            if (CustomDataEntries != null && CustomDataEntries.Count > 0)
            {
                MyIniParseResult result;
                if (!_customData.TryParse(BlockInstance.CustomData, out result))
                {
                    _program.Echo($"Cannot Parse Custom Data in: {BlockInstance.CustomName}");
                    throw new Exception(result.ToString());
                }
            }
        }
    }
}
