using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace IngameScript
{
    internal class FarmGroup
    {
        public string GroupName { get; }
        public List<FarmPlot> FarmPlots { get; }
        public List<IrrigationSystem> IrrigationSystems { get; }
        public List<LcdPanel> LcdPanels { get; }
        public List<AirVent> AirVents { get; }
        public StateManager StateManager { get; }

        public FarmGroup(string groupName)
        {
            GroupName = groupName;
            FarmPlots = new List<FarmPlot>();
            IrrigationSystems = new List<IrrigationSystem>();
            LcdPanels = new List<LcdPanel>();
            AirVents = new List<AirVent>();
            StateManager = new StateManager();
        }

        public void Clear()
        {
            FarmPlots.Clear();
            IrrigationSystems.Clear();
            LcdPanels.Clear();
            AirVents.Clear();
            StateManager.ClearTimers();
        }
    }

    internal class FarmGroups
    {
        readonly Dictionary<string, FarmGroup> groups = new Dictionary<string, FarmGroup>();
        readonly IMyGridTerminalSystem gridTerminalSystem;
        readonly Program program;

        public FarmGroups(IMyGridTerminalSystem gridTerminalSystem, Program program)
        {
            this.gridTerminalSystem = gridTerminalSystem;
            this.program = program;
        }

        public FarmGroup GetGroup(string groupName)
        {
            if (!groups.ContainsKey(groupName))
            {
                groups[groupName] = new FarmGroup(groupName);
            }
            return groups[groupName];
        }

        public List<string> GetGroupNames()
        {
            return groups.Keys.ToList();
        }

        public void FindBlocks(string groupName, List<LcdPanel> lcdPanels)
        {
            var group = GetGroup(groupName);
            group.Clear();

            lcdPanels.ForEach(panel =>
            {
                group.LcdPanels.Add(panel);
            });

            // Find the block group by name
            IMyBlockGroup blockGroup = gridTerminalSystem.GetBlockGroupWithName(groupName);

            // Find the farm plots in the group
            List<IMyFunctionalBlock> validFarmPlots = new List<IMyFunctionalBlock>();
            blockGroup?.GetBlocksOfType(validFarmPlots, block => FarmPlot.BlockIsValid(block));
            validFarmPlots.ForEach(block => group.FarmPlots.Add(new FarmPlot(block, program)));

            // Find the irrigation systems in the group
            List<IMyGasGenerator> validIrrigationSystems = new List<IMyGasGenerator>();
            blockGroup?.GetBlocksOfType(
                validIrrigationSystems,
                block => IrrigationSystem.BlockIsValid(block)
            );
            validIrrigationSystems.ForEach(block =>
                group.IrrigationSystems.Add(new IrrigationSystem(block, program))
            );

            // Find the air vents in the group
            List<IMyAirVent> validAirVents = new List<IMyAirVent>();
            blockGroup?.GetBlocksOfType(validAirVents, block => AirVent.BlockIsValid(block));
            validAirVents.ForEach(block => group.AirVents.Add(new AirVent(block, program)));

            //Find the timers in the group
            List<IMyTimerBlock> validTimers = new List<IMyTimerBlock>();
            blockGroup?.GetBlocksOfType(validTimers, block => Timer.BlockIsValid(block));
            validTimers.ForEach(block =>
                group.StateManager.RegisterTimer(new Timer(block, program))
            );
        }

        public List<FarmGroup> GetAllGroups()
        {
            return groups.Values.ToList();
        }

        public int GroupCount => groups.Count;

        public void RemoveGroupsNotInList(List<string> groupNames)
        {
            var groupsToRemove = groups.Keys.Where(key => !groupNames.Contains(key)).ToList();
            groupsToRemove.ForEach(key => groups.Remove(key));
        }
    }
}
