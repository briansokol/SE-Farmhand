using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    internal class Timer : Block
    {
        private readonly IMyTimerBlock _timerBlock;
        protected readonly StringBuilder _lcdOutput = new StringBuilder();

        private readonly Dictionary<string, CustomDataConfig> _customDataConfigs = new Dictionary<
            string,
            CustomDataConfig
        >()
        {
            {
                "OnWaterLowTrue",
                new CustomDataConfig(
                    "On Water Low",
                    "false",
                    "Triggers when any farm plots' water level is low"
                )
            },
            {
                "OnWaterLowFalse",
                new CustomDataConfig(
                    "On Water Not Low",
                    "false",
                    "Triggers when all farm plots' water levels are no longer low"
                )
            },
            {
                "OnIceLowTrue",
                new CustomDataConfig(
                    "On Ice Low",
                    "false",
                    "Triggers when the irrigation systems are low on ice"
                )
            },
            {
                "OnIceLowFalse",
                new CustomDataConfig(
                    "On Ice Not Low",
                    "false",
                    "Triggers when the irrigation systems are no longer low on ice"
                )
            },
            {
                "OnPressurizedTrue",
                new CustomDataConfig(
                    "On Pressurized",
                    "false",
                    "Triggers when air vents are pressurized"
                )
            },
            {
                "OnPressurizedFalse",
                new CustomDataConfig(
                    "On Depressurized",
                    "false",
                    "Triggers when air vents are depressurizing"
                )
            },
            {
                "OnCropReady",
                new CustomDataConfig(
                    "On Crop Ready",
                    "false",
                    "Triggers when a farm plot crop is ready for harvest"
                )
            },
            {
                "OnCropDead",
                new CustomDataConfig(
                    "On Crop Dead",
                    "false",
                    "Triggers when a farm plot crop has died"
                )
            },
            {
                "OnCropAvailable",
                new CustomDataConfig(
                    "On Crop Available",
                    "false",
                    "Triggers when a farm plot crop is available for harvest"
                )
            },
        };

        protected override IMyFunctionalBlock BlockInstance => _timerBlock;
        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs =>
            _customDataConfigs;

        /// <summary>
        /// Initializes a new instance of the Timer class
        /// </summary>
        /// <param name="timerBlock">The Space Engineers timer block to wrap</param>
        /// <param name="program">The parent grid program instance</param>
        public Timer(IMyTimerBlock timerBlock, MyGridProgram program)
            : base(program)
        {
            _timerBlock = timerBlock;
            UpdateCustomData();
        }

        /// <summary>
        /// Triggers the timer if the specified event is armed in custom data
        /// </summary>
        /// <param name="theEvent">The event to trigger</param>
        public void Trigger(string theEvent)
        {
            if (IsFunctional() && ShouldTrigger(theEvent))
            {
                _timerBlock.StartCountdown();
            }
        }

        /// <summary>
        /// Checks if a specific event should trigger this timer
        /// </summary>
        /// <param name="theEvent">The event to check</param>
        /// <returns>True if the event is set to be armed</returns>
        private bool ShouldTrigger(string theEvent)
        {
            try
            {
                ParseCustomData();
                return _customData
                    .Get(_customDataHeader, _customDataConfigs[theEvent].Label)
                    .ToBoolean(false);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates whether a block can be used as a timer
        /// </summary>
        /// <param name="block">The timer block to validate</param>
        /// <returns>True if the block can be used as a timer</returns>
        public static bool BlockIsValid(IMyTimerBlock block)
        {
            return IsBlockValid(block);
        }
    }
}
