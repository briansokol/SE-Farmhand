using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    /// <summary>
    /// Manages water/ice processing systems for farm irrigation using hydrogen/oxygen generators
    /// </summary>
    internal class IrrigationSystem : Block
    {
        private readonly IMyGasGenerator _irrigationSystem;
        private readonly IMyInventory _inventory;

        public override IMyTerminalBlock BlockInstance => _irrigationSystem;
        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs => null;

        /// <summary>
        /// Initializes a new instance of the IrrigationSystem class
        /// </summary>
        /// <param name="irrigationSystem">The Space Engineers gas generator block to use as irrigation system</param>
        /// <param name="program">The parent grid program instance</param>
        public IrrigationSystem(IMyGasGenerator irrigationSystem, MyGridProgram program)
            : base(program)
        {
            _irrigationSystem = irrigationSystem;

            foreach (MyComponentBase comp in _irrigationSystem.Components)
            {
                if (_inventory == null)
                {
                    _inventory = comp as IMyInventory;
                }
            }
        }

        /// <summary>
        /// Gets the current volume of ice in the irrigation system
        /// </summary>
        public float CurrentVolume =>
            IsFunctional() && _inventory != null ? (float)_inventory.CurrentVolume : 0f;

        /// <summary>
        /// Gets the maximum storage capacity of the irrigation system
        /// </summary>
        public float MaxVolume =>
            IsFunctional() && _inventory != null ? (float)_inventory.MaxVolume : 0f;

        /// <summary>
        /// Validates whether a gas generator block can be used as an irrigation system
        /// </summary>
        /// <param name="block">The gas generator block to validate</param>
        /// <returns>True if the block can be used as an irrigation system</returns>
        public static bool BlockIsValid(IMyTerminalBlock block)
        {
            if (!(block is IMyGasGenerator) || !IsBlockValid(block))
            {
                return false;
            }

            foreach (MyComponentBase comp in block.Components)
            {
                var resourceSourceComponent = comp as MyResourceSourceComponent;
                if (
                    resourceSourceComponent != null
                    && resourceSourceComponent.ResourceTypes.Any(r => r.SubtypeName == "Water")
                )
                {
                    return true;
                }
            }
            return false;
        }
    }
}
