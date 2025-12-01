using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;

namespace IngameScript
{
    /// <summary>
    /// Manages water storage tanks for farm water supply monitoring
    /// </summary>
    internal class WaterTank : Block
    {
        private readonly IMyGasTank _waterTank;

        public override IMyTerminalBlock BlockInstance => _waterTank;
        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs => null;

        /// <summary>
        /// Initializes a new instance of the WaterTank class
        /// </summary>
        /// <param name="waterTank">The Space Engineers gas tank block to use as water tank</param>
        /// <param name="program">The parent grid program instance</param>
        public WaterTank(IMyGasTank waterTank, MyGridProgram program)
            : base(program)
        {
            _waterTank = waterTank;
        }

        /// <summary>
        /// Gets the current volume of water in the tank (in liters)
        /// </summary>
        public double CurrentVolume =>
            IsFunctional() && _waterTank != null
                ? _waterTank.FilledRatio * _waterTank.Capacity
                : 0.0;

        /// <summary>
        /// Gets the maximum storage capacity of the tank (in liters)
        /// </summary>
        public double MaxVolume => IsFunctional() && _waterTank != null ? _waterTank.Capacity : 0.0;

        /// <summary>
        /// Gets the filled ratio (0.0 to 1.0)
        /// </summary>
        public double FilledRatio =>
            IsFunctional() && _waterTank != null ? _waterTank.FilledRatio : 0.0;

        /// <summary>
        /// Validates whether a gas tank block can be used as a water tank
        /// </summary>
        /// <param name="block">The gas tank block to validate</param>
        /// <returns>True if the block can be used as a water tank</returns>
        public static bool BlockIsValid(IMyTerminalBlock block)
        {
            if (!(block is IMyGasTank) || !IsBlockValid(block))
            {
                return false;
            }

            // Validate that the tank stores water by checking resource source component
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
