using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace IngameScript
{
    /// <summary>
    /// Manages air vent blocks for atmospheric monitoring and pressure control
    /// </summary>
    internal class AirVent : Block
    {
        private readonly IMyAirVent _airVent;

        public override IMyTerminalBlock BlockInstance => _airVent;
        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs => null;

        /// <summary>
        /// Initializes a new instance of the AirVent class
        /// </summary>
        /// <param name="airVent">The Space Engineers air vent block to wrap</param>
        /// <param name="program">The parent grid program instance</param>
        public AirVent(IMyAirVent airVent, MyGridProgram program)
            : base(program)
        {
            _airVent = airVent;
        }

        /// <summary>
        /// Gets the current oxygen level in the connected room
        /// </summary>
        public float OxygenLevel => IsFunctional() ? _airVent.GetOxygenLevel() : 0f;

        /// <summary>
        /// Gets the current pressurization status of the air vent
        /// </summary>
        public VentStatus Status => IsFunctional() ? _airVent.Status : VentStatus.Depressurized;

        /// <summary>
        /// Gets whether the room can be pressurized (is air tight)
        /// </summary>
        public bool CanPressurize => IsFunctional() && _airVent.CanPressurize;

        /// <summary>
        /// Validates whether a block can be used as an air vent
        /// </summary>
        public static bool BlockIsValid(IMyTerminalBlock block)
        {
            return block is IMyAirVent && (block as IMyAirVent).Enabled && IsBlockValid(block);
        }
    }
}
