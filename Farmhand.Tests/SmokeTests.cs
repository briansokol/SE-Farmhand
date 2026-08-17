using FluentAssertions;
using IngameScript;
using Xunit;

namespace Farmhand.Tests
{
    /// <summary>Verifies the test project can see and construct types from the script assembly.</summary>
    public class SmokeTests
    {
        [Fact]
        public void FarmStats_NewInstance_HasEmptyCollections()
        {
            var stats = new FarmStats();

            stats.CausesOfDeath.Should().BeEmpty();
            stats.PlotSummary.Should().BeEmpty();
            stats.YieldSummary.Should().BeEmpty();
            stats.GrowthSummary.Should().BeEmpty();
            stats.AlertMessages.Should().BeEmpty();
            stats.SeedsNeeded.Should().Be(0);
        }
    }
}
