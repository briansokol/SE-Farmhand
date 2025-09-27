using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace IngameScript
{
    /// <summary>
    /// Represents a collection of farm-related blocks organized by group name
    /// </summary>
    internal class FarmGroup
    {
        public string GroupName { get; }
        public List<FarmPlot> FarmPlots { get; }
        public List<IrrigationSystem> IrrigationSystems { get; }
        public List<LcdPanel> LcdPanels { get; }
        public List<Cockpit> Cockpits { get; }
        public List<AirVent> AirVents { get; }
        public StateManager StateManager { get; }

        /// <summary>
        /// Initializes a new farm group with the specified name
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        public FarmGroup(string groupName)
        {
            GroupName = groupName;
            FarmPlots = new List<FarmPlot>();
            IrrigationSystems = new List<IrrigationSystem>();
            LcdPanels = new List<LcdPanel>();
            Cockpits = new List<Cockpit>();
            AirVents = new List<AirVent>();
            StateManager = new StateManager();
        }

        /// <summary>
        /// Clears all blocks from this farm group
        /// </summary>
        public void Clear()
        {
            FarmPlots.Clear();
            IrrigationSystems.Clear();
            LcdPanels.Clear();
            Cockpits.Clear();
            AirVents.Clear();
            StateManager.ClearTimers();
        }
    }

    /// <summary>
    /// Manages multiple farm groups and provides block discovery and organization functionality
    /// </summary>
    internal class FarmGroups
    {
        readonly Dictionary<string, FarmGroup> groups = new Dictionary<string, FarmGroup>();
        readonly IMyGridTerminalSystem gridTerminalSystem;
        readonly Program program;

        /// <summary>
        /// Initializes a new farm groups manager
        /// </summary>
        /// <param name="gridTerminalSystem">The grid terminal system for block discovery</param>
        /// <param name="program">The parent program instance</param>
        public FarmGroups(IMyGridTerminalSystem gridTerminalSystem, Program program)
        {
            this.gridTerminalSystem = gridTerminalSystem;
            this.program = program;
        }

        /// <summary>
        /// Gets or creates a farm group with the specified name
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        /// <returns>The farm group instance</returns>
        public FarmGroup GetGroup(string groupName)
        {
            if (!groups.ContainsKey(groupName))
            {
                groups[groupName] = new FarmGroup(groupName);
            }
            return groups[groupName];
        }

        /// <summary>
        /// Gets all currently registered farm group names
        /// </summary>
        /// <returns>List of farm group names</returns>
        public List<string> GetGroupNames()
        {
            return groups.Keys.ToList();
        }

        /// <summary>
        /// Discovers and registers all farm-related blocks for the specified group
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        /// <param name="lcdPanels">LCD panels to add to the group</param>
        /// <param name="cockpits">Cockpits to add to the group</param>
        public void FindBlocks(string groupName, List<LcdPanel> lcdPanels, List<Cockpit> cockpits)
        {
            var group = GetGroup(groupName);
            group.Clear();

            group.LcdPanels.AddRange(lcdPanels);
            group.Cockpits.AddRange(cockpits);

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

        /// <summary>
        /// Gets all registered farm groups
        /// </summary>
        /// <returns>List of all farm groups</returns>
        public List<FarmGroup> GetAllGroups()
        {
            return groups.Values.ToList();
        }

        public int GroupCount => groups.Count;

        /// <summary>
        /// Removes farm groups that are not in the specified list
        /// </summary>
        /// <param name="groupNames">List of group names to keep</param>
        public void RemoveGroupsNotInList(List<string> groupNames)
        {
            var groupsToRemove = groups.Keys.Where(key => !groupNames.Contains(key)).ToList();
            groupsToRemove.ForEach(key => groups.Remove(key));
        }
    }
}
