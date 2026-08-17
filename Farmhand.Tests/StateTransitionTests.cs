using FluentAssertions;
using IngameScript;
using Xunit;

namespace Farmhand.Tests
{
    /// <summary>
    /// Verifies that state change detection fires only on genuine transitions.
    /// These events drive real in-game hardware, so a spurious fire is a visible bug.
    /// A state seen for the first time is deliberately NOT a transition, which is what
    /// suppresses an event storm on script load or recompile.
    /// </summary>
    public class StateTransitionTests
    {
        [Fact]
        public void FirstObservation_IsNotATransition()
        {
            var manager = new StateManager();
            manager.HasStateChanged("OnWaterLow", true).Should().BeFalse();
        }

        [Fact]
        public void RepeatedSameValue_IsNotATransition()
        {
            var manager = new StateManager();
            manager.UpdateState("OnWaterLow", true);
            manager.HasStateChanged("OnWaterLow", true).Should().BeFalse();
        }

        [Fact]
        public void ChangedValue_IsATransition()
        {
            var manager = new StateManager();
            manager.UpdateState("OnWaterLow", true);
            manager.HasStateChanged("OnWaterLow", false).Should().BeTrue();
        }

        [Fact]
        public void FlipBack_IsATransitionAgain()
        {
            var manager = new StateManager();
            manager.UpdateState("OnWaterLow", true);
            manager.UpdateState("OnWaterLow", false);
            manager.HasStateChanged("OnWaterLow", true).Should().BeTrue();
        }

        [Fact]
        public void DistinctStatesAreTrackedIndependently()
        {
            var manager = new StateManager();
            manager.UpdateState("OnWaterLow", true);

            // OnCropReady has never been seen, so it is not a transition yet.
            manager.HasStateChanged("OnCropReady", true).Should().BeFalse();
            manager.HasStateChanged("OnWaterLow", false).Should().BeTrue();
        }

        [Fact]
        public void UpdateStateRecordsValue_EvenWhenUnchanged()
        {
            var manager = new StateManager();
            manager.UpdateState("OnIceLow", false);
            manager.GetPreviousState("OnIceLow").Should().BeFalse();

            manager.UpdateState("OnIceLow", true);
            manager.GetPreviousState("OnIceLow").Should().BeTrue();
        }
    }
}
