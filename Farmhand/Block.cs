using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    /// <summary>
    /// Represents custom data configuration for a block
    /// </summary>
    internal struct CustomDataConfig
    {
        public string Label { get; }
        public string DefaultValue { get; }
        public string Comment { get; }

        public CustomDataConfig(string label, string defaultValue, string comment = null)
        {
            Label = label;
            DefaultValue = defaultValue;
            Comment = comment;
        }
    }

    internal abstract class Block
    {
        protected readonly MyIni _customData = new MyIni();
        protected readonly string _customDataHeader = "Farmhand";
        protected readonly MyGridProgram _program;

        // Abstract properties that must be implemented by derived classes
        protected abstract IMyFunctionalBlock BlockInstance { get; }
        protected abstract Dictionary<string, CustomDataConfig> CustomDataConfigs { get; }

        protected Block(MyGridProgram program)
        {
            _program = program;
        }

        /// <summary>
        /// Checks if the block is functional (not null, not closed, and enabled)
        /// </summary>
        public bool IsFunctional()
        {
            return BlockInstance != null && !BlockInstance.Closed && BlockInstance.Enabled;
        }

        /// <summary>
        /// Generic validation method for blocks that only need basic validation
        /// </summary>
        protected static bool IsBlockValid<T>(T block)
            where T : class, IMyFunctionalBlock
        {
            return block != null && !block.Closed && block.Enabled;
        }

        protected void UpdateCustomData()
        {
            if (CustomDataConfigs != null && CustomDataConfigs.Count > 0 && IsFunctional())
            {
                ParseCustomData();
                foreach (KeyValuePair<string, CustomDataConfig> entry in CustomDataConfigs)
                {
                    _customData.Set(
                        _customDataHeader,
                        entry.Value.Label,
                        _customData
                            .Get(_customDataHeader, entry.Value.Label)
                            .ToString(entry.Value.DefaultValue)
                    );
                    if (!string.IsNullOrEmpty(entry.Value.Comment))
                    {
                        _customData.SetComment(
                            _customDataHeader,
                            entry.Value.Label,
                            entry.Value.Comment
                        );
                    }
                }
                BlockInstance.CustomData = _customData.ToString();
            }
        }

        public void ParseCustomData()
        {
            if (CustomDataConfigs != null && CustomDataConfigs.Count > 0 && IsFunctional())
            {
                MyIniParseResult result;
                if (!_customData.TryParse(BlockInstance.CustomData, out result))
                {
                    _program.Echo($"Cannot Parse Custom Data in: {BlockInstance.CustomName}");
                    // throw new Exception(result.ToString());
                }
            }
        }
    }
}
