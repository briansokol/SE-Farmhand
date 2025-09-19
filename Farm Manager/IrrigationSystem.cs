using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;

namespace IngameScript
{
    internal class IrrigationSystem : Block
    {
        private readonly IMyGasGenerator _irrigationSystem;
        private readonly IMyInventory _inventory;

        private readonly Dictionary<string, string> _customDataEntries =
            new Dictionary<string, string>();

        protected override IMyFunctionalBlock BlockInstance => _irrigationSystem;
        protected override Dictionary<string, string> CustomDataEntries => _customDataEntries;

        public IrrigationSystem(IMyGasGenerator irrigationSystem, MyGridProgram program)
            : base(program)
        {
            _irrigationSystem = irrigationSystem;

            foreach (MyComponentBase comp in _irrigationSystem.Components)
            {
                if (_inventory == null)
                    _inventory = comp as IMyInventory;
            }
        }

        public float CurrentVolume =>
            IsFunctional() && _inventory != null ? (float)_inventory.CurrentVolume : 0f;

        public float MaxVolume =>
            IsFunctional() && _inventory != null ? (float)_inventory.MaxVolume : 0f;

        public static bool BlockIsValid(IMyGasGenerator block)
        {
            if (!IsValid(block))
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
