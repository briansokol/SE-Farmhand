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
        /// <summary>
        /// The cycle all per-block caches are gated on, published once per cycle by Program.
        /// Static because Block holds its host as MyGridProgram, and a programmable block
        /// script has exactly one Program instance.
        /// </summary>
        internal static int CurrentCycle;

        protected readonly MyIni _customData = new MyIni();
        protected readonly string _customDataHeader = "Farmhand";
        protected readonly MyGridProgram _program;
        int _checkedCycle = -1;
        string _lastRaw;

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
        /// True when the underlying block still exists on the grid, regardless of whether it
        /// is switched on. Distinct from IsFunctional(), which also requires Enabled, and is
        /// therefore wrong for cache invalidation: a player switching a block off must not
        /// discard its wrapper or trigger a rediscovery.
        /// </summary>
        public bool IsPresent()
        {
            return BlockInstance != null && !BlockInstance.Closed;
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
        internal void UpdateCustomData()
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
                string rendered = _customData.ToString();
                if (rendered != BlockInstance.CustomData)
                {
                    BlockInstance.CustomData = rendered;
                }
                // Record what we just wrote (or confirmed) so the next cycle does not mistake
                // our own write for an external edit and re-parse needlessly.
                _lastRaw = rendered;
            }
        }

        /// <summary>
        /// Ensures the parsed INI view matches the block's CustomData.
        /// Reads the raw string at most once per cycle and re-parses only when it actually
        /// changed, so repeated getter calls within a cycle cost one integer comparison.
        /// </summary>
        public void ParseCustomData()
        {
            if (CustomDataConfigs == null || CustomDataConfigs.Count == 0 || !IsFunctional())
            {
                return;
            }

            if (_checkedCycle == CurrentCycle)
            {
                return;
            }
            _checkedCycle = CurrentCycle;

            string raw = BlockInstance.CustomData;
            if (raw == _lastRaw)
            {
                return;
            }

            MyIniParseResult result;
            if (!_customData.TryParse(raw, out result))
            {
                _program.Echo($"Cannot Parse Custom Data in: {BlockInstance.CustomName}");
            }
            _lastRaw = raw;
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
