using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;

namespace IngameScript
{
    internal class SolarFoodGenerator : Block
    {
        private readonly IMyFunctionalBlock _block;
        private readonly IMySolarFoodGenerator _solarFoodGenerator;

        public override IMyTerminalBlock BlockInstance => _block;

        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs => null;

        public float TimeRemainingUntilNextBatch =>
            IsFunctional() && _solarFoodGenerator != null
                ? _solarFoodGenerator.TimeRemainingUntilNextBatch
                : 0f;

        public float ItemsPerMinute =>
            IsFunctional() && _solarFoodGenerator != null ? _solarFoodGenerator.ItemsPerMinute : 0f;

        public SolarFoodGenerator(IMyFunctionalBlock block, MyGridProgram program)
            : base(program)
        {
            _block = block;

            // Extract the solar food generator component
            foreach (MyComponentBase comp in _block.Components)
            {
                if (_solarFoodGenerator == null)
                {
                    _solarFoodGenerator = comp as IMySolarFoodGenerator;
                }
            }
        }

        public static bool BlockIsValid(IMyTerminalBlock block)
        {
            if (!(block is IMyFunctionalBlock) || !IsBlockValid(block))
            {
                return false;
            }

            // Validate that the block has a solar food generator component
            foreach (MyComponentBase comp in block.Components)
            {
                var solarFoodGenerator = comp as IMySolarFoodGenerator;
                if (solarFoodGenerator != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
