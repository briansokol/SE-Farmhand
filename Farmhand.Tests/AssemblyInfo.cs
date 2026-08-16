using Xunit;

// StateManager holds List<Timer>, which forces layout of Block._customData (MyIni, from
// VRage.Game). When that races another collection resolving the same types via FarmPlot,
// the Mono net48 test host aborts. Every test passes in isolation and the suite runs in
// roughly 300ms, so serialising it costs nothing. A programmable block script is
// single-threaded in game, so this masks nothing about production behaviour.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
