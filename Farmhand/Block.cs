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

    /// <summary>
    /// Abstract base class for all Space Engineers block wrappers with custom data management
    /// </summary>
    internal abstract class Block
    {
        protected readonly MyIni _customData = new MyIni();
        protected readonly string _customDataHeader = "Farmhand";
        protected readonly MyGridProgram _program;

        // Abstract properties that must be implemented by derived classes
        public abstract IMyTerminalBlock BlockInstance { get; }
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
            return IsBlockValid(BlockInstance)
                && (
                    !(BlockInstance is IMyFunctionalBlock)
                    || (BlockInstance as IMyFunctionalBlock).Enabled
                );
        }

        /// <summary>
        /// Gets the custom name of the farm plot block
        /// </summary>
        public string CustomName =>
            IsBlockValid(BlockInstance) ? BlockInstance.CustomName : "NOT VALID";

        /// <summary>
        /// Generic validation method for blocks that only need basic validation
        /// </summary>
        protected static bool IsBlockValid<T>(T block)
            where T : class, IMyTerminalBlock
        {
            if (block is IMyFunctionalBlock)
            {
                return block != null
                    && !block.Closed
                    && (block as IMyFunctionalBlock).Enabled
                    && (block as IMyFunctionalBlock).IsFunctional;
            }
            else
            {
                return block != null && !block.Closed;
            }
        }

        /// <summary>
        /// Updates the block's custom data with current configuration values
        /// </summary>
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
                            $"; {entry.Value.Comment}"
                        );
                    }
                }
                _customData.SetSectionComment(
                    _customDataHeader,
                    "; For more detailed explanations of options, see the official guide on Steam"
                );
                BlockInstance.CustomData = _customData.ToString();
            }
        }

        /// <summary>
        /// Parses custom data from the block's CustomData property into the internal INI structure
        /// </summary>
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

        /// <summary>
        /// Removes obsolete custom data keys that are not valid for this block type
        /// </summary>
        public void CleanupCustomData()
        {
            // Skip blocks with no custom data configuration
            if (CustomDataConfigs == null || CustomDataConfigs.Count == 0 || !IsFunctional())
            {
                return;
            }

            // Parse existing custom data
            ParseCustomData();

            // Get all keys in the Farmhand section
            List<MyIniKey> keys = new List<MyIniKey>();
            _customData.GetKeys(_customDataHeader, keys);

            // Build a set of valid labels for this block type
            HashSet<string> validLabels = new HashSet<string>();
            foreach (KeyValuePair<string, CustomDataConfig> entry in CustomDataConfigs)
            {
                validLabels.Add(entry.Value.Label);
            }

            // Remove keys that are not in the valid set
            bool changed = false;
            foreach (MyIniKey key in keys)
            {
                if (!validLabels.Contains(key.Name))
                {
                    _customData.Delete(_customDataHeader, key.Name);
                    changed = true;
                }
            }

            // Write back to block if changes were made
            if (changed)
            {
                BlockInstance.CustomData = _customData.ToString();
            }
        }

        /// <summary>
        /// Gets a string value from custom data by config key
        /// </summary>
        /// <param name="configKey">The key in CustomDataConfigs dictionary</param>
        /// <param name="defaultValue">Default value if key not found</param>
        /// <returns>The configured string value</returns>
        protected string GetCustomDataString(string configKey, string defaultValue = "")
        {
            if (CustomDataConfigs == null || !CustomDataConfigs.ContainsKey(configKey))
            {
                return defaultValue;
            }

            ParseCustomData();
            return _customData
                .Get(_customDataHeader, CustomDataConfigs[configKey].Label)
                .ToString(defaultValue);
        }

        /// <summary>
        /// Gets a boolean value from custom data by config key
        /// </summary>
        /// <param name="configKey">The key in CustomDataConfigs dictionary</param>
        /// <param name="defaultValue">Default value if key not found</param>
        /// <returns>The configured boolean value</returns>
        protected bool GetCustomDataBool(string configKey, bool defaultValue = false)
        {
            if (CustomDataConfigs == null || !CustomDataConfigs.ContainsKey(configKey))
            {
                return defaultValue;
            }

            try
            {
                ParseCustomData();
                return _customData
                    .Get(_customDataHeader, CustomDataConfigs[configKey].Label)
                    .ToBoolean(defaultValue);
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
