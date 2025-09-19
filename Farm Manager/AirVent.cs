using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace IngameScript
{
    internal class AirVent : Block
    {
        private readonly IMyAirVent _airVent;

        private readonly Dictionary<string, string> _customDataEntries =
            new Dictionary<string, string>();

        protected override IMyFunctionalBlock BlockInstance => _airVent;
        protected override Dictionary<string, string> CustomDataEntries => _customDataEntries;

        public AirVent(IMyAirVent airVent, MyGridProgram program)
            : base(program)
        {
            _airVent = airVent;
        }

        public float OxygenLevel => IsFunctional() ? _airVent.GetOxygenLevel() : 0f;

        public VentStatus Status => IsFunctional() ? _airVent.Status : VentStatus.Depressurized;

        public bool CanPressurize => IsFunctional() && _airVent.CanPressurize;

        public static bool BlockIsValid(IMyAirVent block)
        {
            return IsValid(block);
        }
    }
}
