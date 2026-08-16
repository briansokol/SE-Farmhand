using System;
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
        public List<WaterTank> WaterTanks { get; }
        public List<LcdPanel> LcdPanels { get; }
        public List<TextSurfaceProvider> TextSurfaceProviders { get; }
        public List<AirVent> AirVents { get; }
        public List<SolarFoodGenerator> SolarFoodGenerators { get; }
        public List<BroadcastController> BroadcastControllers { get; }
        public StateManager StateManager { get; }
        public FarmStats Stats { get; set; }

        /// <summary>
        /// Buffer the chunked scan steps accumulate into. Displays read Stats, so a scan
        /// spread over several ticks never exposes a half-built snapshot.
        /// </summary>
        public FarmStats ScratchStats { get; set; }
        public ProgrammableBlock ProgrammableBlock { get; set; }
        public int RunNumber { get; set; }

        // Persistent EntityId to wrapper indexes. Wrappers must survive between cycles so the
        // per-cycle caches on Block and FarmPlot are not thrown away with them.
        public readonly Dictionary<long, FarmPlot> FarmPlotsById = new Dictionary<long, FarmPlot>();
        public readonly Dictionary<long, IrrigationSystem> IrrigationSystemsById =
            new Dictionary<long, IrrigationSystem>();
        public readonly Dictionary<long, WaterTank> WaterTanksById =
            new Dictionary<long, WaterTank>();
        public readonly Dictionary<long, AirVent> AirVentsById = new Dictionary<long, AirVent>();
        public readonly Dictionary<long, SolarFoodGenerator> SolarFoodGeneratorsById =
            new Dictionary<long, SolarFoodGenerator>();

        /// <summary>
        /// Initializes a new farm group with the specified name
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        public FarmGroup(string groupName)
        {
            GroupName = groupName;
            FarmPlots = new List<FarmPlot>();
            IrrigationSystems = new List<IrrigationSystem>();
            WaterTanks = new List<WaterTank>();
            LcdPanels = new List<LcdPanel>();
            TextSurfaceProviders = new List<TextSurfaceProvider>();
            AirVents = new List<AirVent>();
            SolarFoodGenerators = new List<SolarFoodGenerator>();
            BroadcastControllers = new List<BroadcastController>();
            StateManager = new StateManager();
            Stats = new FarmStats();
            ScratchStats = new FarmStats();
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

        // Scratch buffers hoisted to fields so discovery does not allocate a fresh list per call.
        // Each is fully consumed by Reconcile before the next Find* method reuses it.
        readonly List<long> _staleIds = new List<long>();
        readonly List<IMyFunctionalBlock> _scratchFunctional = new List<IMyFunctionalBlock>();
        readonly List<IMyGasGenerator> _scratchGasGenerator = new List<IMyGasGenerator>();
        readonly List<IMyGasTank> _scratchGasTank = new List<IMyGasTank>();
        readonly List<IMyAirVent> _scratchAirVent = new List<IMyAirVent>();

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
        public void ResetBlocks(string groupName, List<LcdPanel> lcdPanels, List<TextSurfaceProvider> textSurfaceProviders)
        {
            var group = GetGroup(groupName);

            group.LcdPanels.Clear();
            group.TextSurfaceProviders.Clear();

            group.LcdPanels.AddRange(lcdPanels);
            group.TextSurfaceProviders.AddRange(textSurfaceProviders);
        }

        /// <summary>
        /// Reconciles a wrapper list against the current block set, reusing existing wrapper
        /// instances by EntityId. Wrapper persistence is required for the per-cycle caches on
        /// Block and FarmPlot to survive between cycles.
        /// </summary>
        /// <param name="found">Blocks discovered this pass.</param>
        /// <param name="existing">Wrapper list to rebuild in place.</param>
        /// <param name="byId">Persistent EntityId to wrapper index for this list.</param>
        /// <param name="create">Factory for wrappers not yet seen.</param>
        void Reconcile<TBlock, TWrapper>(
            List<TBlock> found,
            List<TWrapper> existing,
            Dictionary<long, TWrapper> byId,
            Func<TBlock, TWrapper> create
        )
            where TBlock : class, IMyTerminalBlock
            where TWrapper : Block
        {
            existing.Clear();
            for (int i = 0; i < found.Count; i++)
            {
                TBlock block = found[i];
                long id = block.EntityId;
                TWrapper wrapper;
                if (!byId.TryGetValue(id, out wrapper))
                {
                    wrapper = create(block);
                    byId[id] = wrapper;
                }
                else
                {
                    // Reused wrapper: refresh its INI template so a key the player deleted
                    // is restored. Wrapper constructors call UpdateCustomData, so before
                    // wrappers persisted this happened every cycle as a side effect of
                    // rebuilding them. Doing it here keeps that self-healing at discovery
                    // cadence instead of losing it entirely.
                    wrapper.UpdateCustomData();
                }
                existing.Add(wrapper);
            }

            // Evict wrappers whose blocks have left the grid, so the index does not grow
            // without bound. IsPresent, not IsFunctional: a disabled block is still present
            // and must keep its cached parse.
            if (byId.Count != existing.Count)
            {
                _staleIds.Clear();
                foreach (KeyValuePair<long, TWrapper> entry in byId)
                {
                    if (!entry.Value.IsPresent())
                    {
                        _staleIds.Add(entry.Key);
                    }
                }
                for (int i = 0; i < _staleIds.Count; i++)
                {
                    byId.Remove(_staleIds[i]);
                }
            }
        }

        /// <summary>
        /// Discovers and registers farm plots for the specified group
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        public void FindFarmPlots(string groupName)
        {
            var group = GetGroup(groupName);
            IMyBlockGroup blockGroup = gridTerminalSystem.GetBlockGroupWithName(groupName);
            if (blockGroup == null)
            {
                group.FarmPlots.Clear();
                return;
            }

            _scratchFunctional.Clear();
            blockGroup.GetBlocksOfType(_scratchFunctional, block => FarmPlot.BlockIsValid(block));

            Reconcile(
                _scratchFunctional,
                group.FarmPlots,
                group.FarmPlotsById,
                block => new FarmPlot(block, program)
            );
        }

        /// <summary>
        /// Discovers and registers irrigation systems for the specified group
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        public void FindIrrigationSystems(string groupName)
        {
            var group = GetGroup(groupName);
            IMyBlockGroup blockGroup = gridTerminalSystem.GetBlockGroupWithName(groupName);
            if (blockGroup == null)
            {
                group.IrrigationSystems.Clear();
                return;
            }

            _scratchGasGenerator.Clear();
            blockGroup.GetBlocksOfType(
                _scratchGasGenerator,
                block => IrrigationSystem.BlockIsValid(block)
            );

            Reconcile(
                _scratchGasGenerator,
                group.IrrigationSystems,
                group.IrrigationSystemsById,
                block => new IrrigationSystem(block, program)
            );
        }

        /// <summary>
        /// Discovers and registers water tanks for the specified group
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        public void FindWaterTanks(string groupName)
        {
            var group = GetGroup(groupName);
            IMyBlockGroup blockGroup = gridTerminalSystem.GetBlockGroupWithName(groupName);
            if (blockGroup == null)
            {
                group.WaterTanks.Clear();
                return;
            }

            _scratchGasTank.Clear();
            blockGroup.GetBlocksOfType(_scratchGasTank, block => WaterTank.BlockIsValid(block));

            Reconcile(
                _scratchGasTank,
                group.WaterTanks,
                group.WaterTanksById,
                block => new WaterTank(block, program)
            );
        }

        /// <summary>
        /// Discovers and registers air vents for the specified group
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        public void FindAirVents(string groupName)
        {
            var group = GetGroup(groupName);
            IMyBlockGroup blockGroup = gridTerminalSystem.GetBlockGroupWithName(groupName);
            if (blockGroup == null)
            {
                group.AirVents.Clear();
                return;
            }

            _scratchAirVent.Clear();
            blockGroup.GetBlocksOfType(_scratchAirVent, block => AirVent.BlockIsValid(block));

            Reconcile(
                _scratchAirVent,
                group.AirVents,
                group.AirVentsById,
                block => new AirVent(block, program)
            );
        }

        /// <summary>
        /// Discovers and registers solar food generators for the specified group
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        public void FindSolarFoodGenerators(string groupName)
        {
            var group = GetGroup(groupName);
            IMyBlockGroup blockGroup = gridTerminalSystem.GetBlockGroupWithName(groupName);
            if (blockGroup == null)
            {
                group.SolarFoodGenerators.Clear();
                return;
            }

            _scratchFunctional.Clear();
            blockGroup.GetBlocksOfType(
                _scratchFunctional,
                block => SolarFoodGenerator.BlockIsValid(block)
            );

            Reconcile(
                _scratchFunctional,
                group.SolarFoodGenerators,
                group.SolarFoodGeneratorsById,
                block => new SolarFoodGenerator(block, program)
            );
        }

        /// <summary>
        /// Discovers and registers timers for the specified group
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        public void FindTimers(string groupName)
        {
            var group = GetGroup(groupName);
            IMyBlockGroup blockGroup = gridTerminalSystem.GetBlockGroupWithName(groupName);

            group.StateManager.ClearTimers();

            List<IMyTimerBlock> validTimers = new List<IMyTimerBlock>();
            blockGroup?.GetBlocksOfType(validTimers, block => Timer.BlockIsValid(block));
            validTimers.ForEach(block =>
                group.StateManager.RegisterTimer(new Timer(block, program))
            );
        }

        /// <summary>
        /// Discovers and registers action relays for the specified group
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        public void FindActionRelays(string groupName)
        {
            var group = GetGroup(groupName);
            IMyBlockGroup blockGroup = gridTerminalSystem.GetBlockGroupWithName(groupName);

            group.StateManager.ClearActionRelays();

            List<IMyTransponder> validActionRelays = new List<IMyTransponder>();
            blockGroup?.GetBlocksOfType(
                validActionRelays,
                block => ActionRelay.BlockIsValid(block)
            );
            validActionRelays.ForEach(block =>
                group.StateManager.RegisterActionRelay(new ActionRelay(block, program))
            );
        }

        /// <summary>
        /// Discovers and registers broadcast controllers for the specified group
        /// </summary>
        /// <param name="groupName">Name of the farm group</param>
        public void FindBroadcastControllers(string groupName)
        {
            var group = GetGroup(groupName);
            IMyBlockGroup blockGroup = gridTerminalSystem.GetBlockGroupWithName(groupName);

            group.StateManager.ClearBroadcastControllers();

            List<IMyBroadcastController> validBroadcastControllers =
                new List<IMyBroadcastController>();
            blockGroup?.GetBlocksOfType(
                validBroadcastControllers,
                block => BroadcastController.BlockIsValid(block)
            );
            validBroadcastControllers.ForEach(block =>
                group.StateManager.RegisterBroadcastController(
                    new BroadcastController(block, program)
                )
            );
        }
        /// <summary>
        /// Returns the live group collection. Callers must not modify the collection while
        /// enumerating it, and must not suspend mid-enumeration: pipeline steps that yield
        /// walk Program's per-cycle group snapshot instead, refreshed in StepRoot(). Returning
        /// the values view avoids a list allocation on every call.
        /// </summary>

        public IEnumerable<FarmGroup> GetAllGroups()
        {
            return groups.Values;
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
