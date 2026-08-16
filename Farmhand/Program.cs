using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;

namespace IngameScript
{
    /// <summary>
    /// Main Space Engineers Programmable Block Script for automated farm management
    /// </summary>
    public partial class Program : MyGridProgram
    {
        readonly FarmGroups farmGroups;
        readonly ProgrammableBlock thisPb;

        readonly string lcdTag = "FarmLCD";
        readonly string plotLcdTag = "PlotLCD";
        readonly List<PlotLCD> plotLcds = new List<PlotLCD>();
        int runNumber = 0;
        readonly string Version = "v1.3.0";
        readonly string PublishedDate = "2026-03-22";

        /// <summary>
        /// Increments once per completed pipeline cycle. Every per-block cache gates on this
        /// rather than on tick count, because a cycle can span many ticks on a large farm and
        /// configuration must stay stable for the whole of it.
        /// </summary>
        public int CycleNumber { get; private set; }

        /// <summary>Set to force a discovery rescan on the next cycle.</summary>
        public bool RescanRequested { get; set; }

        /// <summary>Cycles between periodic discovery rescans.</summary>
        public int RescanIntervalCycles = 30;

        int _lastDiscoveryCycle = int.MinValue;

        // Persistent EntityId to wrapper indexes for the tag-discovered display blocks. These
        // live on Program rather than on FarmGroup because the wrappers are constructed before
        // their group name is known: the group name is read from the wrapper's own custom data.
        readonly Dictionary<long, PlotLCD> _plotLcdsById = new Dictionary<long, PlotLCD>();
        readonly Dictionary<long, LcdPanel> _lcdPanelsById = new Dictionary<long, LcdPanel>();
        readonly Dictionary<long, TextSurfaceProvider> _textSurfaceProvidersById =
            new Dictionary<long, TextSurfaceProvider>();

        /// <summary>
        /// Snapshot of the current farm groups, refreshed once per cycle. Chunked steps walk
        /// this by index rather than enumerating GetAllGroups() directly, because a step that
        /// yields mid-enumeration would be suspended across ticks while discovery mutates the
        /// underlying dictionary, throwing InvalidOperationException on resume.
        /// </summary>
        readonly List<FarmGroup> _groupSnapshot = new List<FarmGroup>();

        /// <summary>
        /// True when block discovery should run this cycle: first run, an explicit request,
        /// or the periodic interval has elapsed.
        /// </summary>
        bool IsDiscoveryDue()
        {
            if (RescanRequested)
                return true;
            if (_lastDiscoveryCycle == int.MinValue)
                return true;
            return CycleNumber - _lastDiscoveryCycle >= RescanIntervalCycles;
        }

        /// <summary>Records that discovery ran this cycle and clears any pending request.</summary>
        void MarkDiscoveryDone()
        {
            _lastDiscoveryCycle = CycleNumber;
            RescanRequested = false;
        }

        bool shiftSprites = false;

        public Program()
        {
            thisPb = new ProgrammableBlock(Me, this);
            farmGroups = new FarmGroups(GridTerminalSystem, this);
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        /// <summary>
        /// Required by Space Engineers API - currently not used
        /// </summary>
        public void Save() { }

        public void Main(string argument)
        {
            if (!string.IsNullOrWhiteSpace(argument))
            {
                DispatchCommand(argument.Trim());
            }

            RunOneTick();
            RenderEchoStatus();
        }

        /// <summary>
        /// Pushes per-cycle animation state onto persistent wrappers. Must run every cycle,
        /// not only when discovery runs, or blinking and multiplayer sprite refresh stall.
        /// </summary>
        void ApplyFrameState()
        {
            // Advance the animation frame. This lived inside PrintDiagnosticHeader, which was
            // only ever called once per cycle from the old queue rebuild. StepRoot now owns
            // frame advancement, so there is exactly one caller.
            runNumber = runNumber >= 5 ? 0 : runNumber + 1;

            foreach (FarmGroup group in farmGroups.GetAllGroups())
            {
                group.RunNumber = runNumber;

                for (int i = 0; i < group.LcdPanels.Count; i++)
                {
                    group.LcdPanels[i].SetShiftSprites(shiftSprites);
                }
                for (int i = 0; i < group.TextSurfaceProviders.Count; i++)
                {
                    group.TextSurfaceProviders[i].SetShiftSprites(shiftSprites);
                }
            }

            for (int i = 0; i < plotLcds.Count; i++)
            {
                plotLcds[i].SetShiftSprites(shiftSprites);
            }
        }

        /// <summary>Refreshes the group snapshot. Called once per cycle from StepRoot.</summary>
        void RefreshGroupSnapshot()
        {
            _groupSnapshot.Clear();
            foreach (FarmGroup group in farmGroups.GetAllGroups())
            {
                _groupSnapshot.Add(group);
            }
        }
    }
}
