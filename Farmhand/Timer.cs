using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace IngameScript
{
    /// <summary>
    /// Event-driven timer automation with state-based triggering for farm operations
    /// </summary>
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
                "TriggerNow",
                new CustomDataConfig(
                    "Trigger Immediately",
                    "true",
                    "Timer will trigger immediately instead of counting down"
                )
            },
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
                "OnCropReadyTrue",
                new CustomDataConfig(
                    "On Crop Ready",
                    "false",
                    "Triggers when any farm plot crop is ready for harvest"
                )
            },
            {
                "OnCropReadyFalse",
                new CustomDataConfig(
                    "On Crop Not Ready",
                    "false",
                    "Triggers when no farm plots are ready for harvest"
                )
            },
            {
                "OnAllCropsReadyTrue",
                new CustomDataConfig(
                    "On All Crops Ready",
                    "false",
                    "Triggers when all planted farm plots with crops are ready for harvest"
                )
            },
            {
                "OnAllCropsReadyFalse",
                new CustomDataConfig(
                    "On All Crops Not Ready",
                    "false",
                    "Triggers when you plant a crop while all other planted plots are ready for harvest"
                )
            },
            {
                "OnCropDyingTrue",
                new CustomDataConfig(
                    "On Crop Dying",
                    "false",
                    "Triggers when at least one farm plot's health is below threshold"
                )
            },
            {
                "OnCropDyingFalse",
                new CustomDataConfig(
                    "On Crop Not Dying",
                    "false",
                    "Triggers when all farm plots' health are above threshold or all have died"
                )
            },
            {
                "OnCropDeadTrue",
                new CustomDataConfig(
                    "On Crop Dead",
                    "false",
                    "Triggers when a farm plot crop has died"
                )
            },
            {
                "OnCropDeadFalse",
                new CustomDataConfig(
                    "On Crop Not Dead",
                    "false",
                    "Triggers when no farm plots have dead crops"
                )
            },
            {
                "OnCropAvailableTrue",
                new CustomDataConfig(
                    "On Crop Available",
                    "false",
                    "Triggers when a farm plot crop is available for planting"
                )
            },
            {
                "OnCropAvailableFalse",
                new CustomDataConfig(
                    "On Crop Not Available",
                    "false",
                    "Triggers when no farm plots have crops available for planting"
                )
            },
        };

        public override IMyTerminalBlock BlockInstance => _timerBlock;
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
                if (ShouldTrigger("TriggerNow"))
                {
                    _timerBlock.Trigger();
                }
                else
                {
                    _timerBlock.StartCountdown();
                }
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
        public static bool BlockIsValid(IMyTerminalBlock block)
        {
            return IsBlockValid(block) && block is IMyTimerBlock;
        }
    }
}
