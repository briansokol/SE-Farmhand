using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace IngameScript
{
    /// <summary>
    /// Event-driven action relay automation with channel-based triggering for farm operations
    /// </summary>
    internal class ActionRelay : Block
    {
        private readonly IMyTransponder _transponderBlock;

        private readonly Dictionary<string, CustomDataConfig> _customDataConfigs = new Dictionary<
            string,
            CustomDataConfig
        >()
        {
            {
                "OnWaterLowTrue",
                new CustomDataConfig(
                    "On Water Low",
                    "0",
                    "Channel (1-100) to signal when any farm plots' water level is low. 0 = disabled"
                )
            },
            {
                "OnWaterLowFalse",
                new CustomDataConfig(
                    "On Water Not Low",
                    "0",
                    "Channel (1-100) to signal when all farm plots' water levels are no longer low. 0 = disabled"
                )
            },
            {
                "OnIceLowTrue",
                new CustomDataConfig(
                    "On Ice Low",
                    "0",
                    "Channel (1-100) to signal when the irrigation systems are low on ice. 0 = disabled"
                )
            },
            {
                "OnIceLowFalse",
                new CustomDataConfig(
                    "On Ice Not Low",
                    "0",
                    "Channel (1-100) to signal when the irrigation systems are no longer low on ice. 0 = disabled"
                )
            },
            {
                "OnPressurizedTrue",
                new CustomDataConfig(
                    "On Pressurized",
                    "0",
                    "Channel (1-100) to signal when air vents are pressurized. 0 = disabled"
                )
            },
            {
                "OnPressurizedFalse",
                new CustomDataConfig(
                    "On Depressurized",
                    "0",
                    "Channel (1-100) to signal when air vents are depressurizing. 0 = disabled"
                )
            },
            {
                "OnCropReadyTrue",
                new CustomDataConfig(
                    "On Any Crop Ready",
                    "0",
                    "Channel (1-100) to signal when any farm plot crop is ready for harvest. 0 = disabled"
                )
            },
            {
                "OnCropReadyFalse",
                new CustomDataConfig(
                    "On No Crops Ready",
                    "0",
                    "Channel (1-100) to signal when no farm plots are ready for harvest. 0 = disabled"
                )
            },
            {
                "OnAllCropsReadyTrue",
                new CustomDataConfig(
                    "On All Crops Ready",
                    "0",
                    "Channel (1-100) to signal when all planted farm plots with crops are ready for harvest. 0 = disabled"
                )
            },
            {
                "OnAllCropsReadyFalse",
                new CustomDataConfig(
                    "On Not All Crops Ready",
                    "0",
                    "Channel (1-100) to signal when you plant a crop while all other planted plots are ready for harvest. 0 = disabled"
                )
            },
            {
                "OnCropDyingTrue",
                new CustomDataConfig(
                    "On Crop Dying",
                    "0",
                    "Channel (1-100) to signal when at least one farm plot's health is below threshold. 0 = disabled"
                )
            },
            {
                "OnCropDyingFalse",
                new CustomDataConfig(
                    "On No Crops Dying",
                    "0",
                    "Channel (1-100) to signal when all farm plots' health are above threshold or all have died. 0 = disabled"
                )
            },
            {
                "OnCropDeadTrue",
                new CustomDataConfig(
                    "On Crop Dead",
                    "0",
                    "Channel (1-100) to signal when a farm plot crop has died. 0 = disabled"
                )
            },
            {
                "OnCropDeadFalse",
                new CustomDataConfig(
                    "On No Dead Crops",
                    "0",
                    "Channel (1-100) to signal when no farm plots have dead crops. 0 = disabled"
                )
            },
            {
                "OnCropAvailableTrue",
                new CustomDataConfig(
                    "On Plot Empty",
                    "0",
                    "Channel (1-100) to signal when a farm plot crop is available for planting. 0 = disabled"
                )
            },
            {
                "OnCropAvailableFalse",
                new CustomDataConfig(
                    "On No Plots Empty",
                    "0",
                    "Channel (1-100) to signal when no farm plots have crops available for planting. 0 = disabled"
                )
            },
        };

        public override IMyTerminalBlock BlockInstance => _transponderBlock;
        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs =>
            _customDataConfigs;

        /// <summary>
        /// Initializes a new instance of the ActionRelay class
        /// </summary>
        /// <param name="transponderBlock">The Space Engineers transponder/action relay block to wrap</param>
        /// <param name="program">The parent grid program instance</param>
        public ActionRelay(IMyTransponder transponderBlock, MyGridProgram program)
            : base(program)
        {
            _transponderBlock = transponderBlock;
            UpdateCustomData();
        }

        /// <summary>
        /// Triggers the action relay to send a signal on the specified channel if the event is configured
        /// </summary>
        /// <param name="theEvent">The event to trigger</param>
        public void Trigger(string theEvent)
        {
            if (IsFunctional())
            {
                int channel = GetChannel(theEvent);
                if (channel > 0 && channel <= 100)
                {
                    _transponderBlock.SendSignal(channel);
                }
            }
        }

        /// <summary>
        /// Gets the channel number configured for a specific event
        /// </summary>
        /// <param name="theEvent">The event to check</param>
        /// <returns>The channel number (0 if disabled or invalid)</returns>
        private int GetChannel(string theEvent)
        {
            try
            {
                ParseCustomData();
                int channel = _customData
                    .Get(_customDataHeader, _customDataConfigs[theEvent].Label)
                    .ToInt32(0);

                // Only return valid channel numbers (1-100), otherwise return 0 (disabled)
                return (channel >= 1 && channel <= 100) ? channel : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Validates whether a block can be used as an action relay
        /// </summary>
        /// <param name="block">The block to validate</param>
        /// <returns>True if the block can be used as an action relay</returns>
        public static bool BlockIsValid(IMyTerminalBlock block)
        {
            return IsBlockValid(block) && block is IMyTransponder;
        }
    }
}
