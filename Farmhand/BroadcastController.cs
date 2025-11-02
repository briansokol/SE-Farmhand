using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.Components;

namespace IngameScript
{
    internal class BroadcastController : Block
    {
        private readonly IMyBroadcastController _block;
        private readonly IMyChatBroadcastControllerComponent _chatComponent;

        private readonly Dictionary<string, CustomDataConfig> _customDataConfigs = new Dictionary<
            string,
            CustomDataConfig
        >()
        {
            {
                "OnWaterLowTrue",
                new CustomDataConfig(
                    "On Water Low",
                    "",
                    "Message to send when any farm plots' water level is low. Leave empty to disable"
                )
            },
            {
                "OnWaterLowFalse",
                new CustomDataConfig(
                    "On Water Not Low",
                    "",
                    "Message to send when all farm plots' water levels are no longer low. Leave empty to disable"
                )
            },
            {
                "OnIceLowTrue",
                new CustomDataConfig(
                    "On Ice Low",
                    "",
                    "Message to send when the irrigation systems are low on ice. Leave empty to disable"
                )
            },
            {
                "OnIceLowFalse",
                new CustomDataConfig(
                    "On Ice Not Low",
                    "",
                    "Message to send when the irrigation systems are no longer low on ice. Leave empty to disable"
                )
            },
            {
                "OnPressurizedTrue",
                new CustomDataConfig(
                    "On Pressurized",
                    "",
                    "Message to send when air vents are pressurized. Leave empty to disable"
                )
            },
            {
                "OnPressurizedFalse",
                new CustomDataConfig(
                    "On Depressurized",
                    "",
                    "Message to send when air vents are depressurizing. Leave empty to disable"
                )
            },
            {
                "OnCropReadyTrue",
                new CustomDataConfig(
                    "On Any Crop Ready",
                    "",
                    "Message to send when any farm plot crop is ready for harvest. Leave empty to disable"
                )
            },
            {
                "OnCropReadyFalse",
                new CustomDataConfig(
                    "On No Crops Ready",
                    "",
                    "Message to send when no farm plots are ready for harvest. Leave empty to disable"
                )
            },
            {
                "OnAllCropsReadyTrue",
                new CustomDataConfig(
                    "On All Crops Ready",
                    "",
                    "Message to send when all planted farm plots with crops are ready for harvest. Leave empty to disable"
                )
            },
            {
                "OnAllCropsReadyFalse",
                new CustomDataConfig(
                    "On Not All Crops Ready",
                    "",
                    "Message to send when you plant a crop while all other planted plots are ready for harvest. Leave empty to disable"
                )
            },
            {
                "OnCropDyingTrue",
                new CustomDataConfig(
                    "On Crop Dying",
                    "",
                    "Message to send when at least one farm plot's health is below threshold. Leave empty to disable"
                )
            },
            {
                "OnCropDyingFalse",
                new CustomDataConfig(
                    "On No Crops Dying",
                    "",
                    "Message to send when all farm plots' health are above threshold or all have died. Leave empty to disable"
                )
            },
            {
                "OnCropDeadTrue",
                new CustomDataConfig(
                    "On Crop Dead",
                    "",
                    "Message to send when a farm plot crop has died. Leave empty to disable"
                )
            },
            {
                "OnCropDeadFalse",
                new CustomDataConfig(
                    "On No Dead Crops",
                    "",
                    "Message to send when no farm plots have dead crops. Leave empty to disable"
                )
            },
            {
                "OnCropAvailableTrue",
                new CustomDataConfig(
                    "On Plot Empty",
                    "",
                    "Message to send when a farm plot crop is available for planting. Leave empty to disable"
                )
            },
            {
                "OnCropAvailableFalse",
                new CustomDataConfig(
                    "On No Plots Empty",
                    "",
                    "Message to send when no farm plots have crops available for planting. Leave empty to disable"
                )
            },
        };

        public override IMyTerminalBlock BlockInstance => _block;

        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs =>
            _customDataConfigs;

        public IMyChatBroadcastControllerComponent ChatComponent =>
            IsFunctional() && _chatComponent != null ? _chatComponent : null;

        public BroadcastController(IMyBroadcastController block, MyGridProgram program)
            : base(program)
        {
            _block = block;

            // Extract the chat broadcast controller component
            foreach (MyComponentBase comp in _block.Components)
            {
                if (_chatComponent == null)
                {
                    _chatComponent = comp as IMyChatBroadcastControllerComponent;
                }
            }

            UpdateCustomData();
        }

        /// <summary>
        /// Triggers the broadcast controller to send a message if the event is configured
        /// </summary>
        /// <param name="theEvent">The event to trigger</param>
        public void Trigger(string theEvent)
        {
            if (IsFunctional() && _chatComponent != null)
            {
                string message = GetMessage(theEvent);
                if (!string.IsNullOrEmpty(message))
                {
                    _chatComponent.SendMessage(message);
                }
            }
        }

        /// <summary>
        /// Gets the message configured for a specific event
        /// </summary>
        /// <param name="theEvent">The event to check</param>
        /// <returns>The message to send (empty string if disabled or invalid)</returns>
        private string GetMessage(string theEvent)
        {
            try
            {
                ParseCustomData();
                string message = _customData
                    .Get(_customDataHeader, _customDataConfigs[theEvent].Label)
                    .ToString("");

                return message ?? "";
            }
            catch
            {
                return "";
            }
        }

        public static bool BlockIsValid(IMyTerminalBlock block)
        {
            return (block is IMyBroadcastController) && IsBlockValid(block);
        }
    }
}
