using FluentAssertions;
using IngameScript;
using Xunit;

namespace Farmhand.Tests
{
    public class FarmStatsTests
    {
        [Fact]
        public void Clear_ResetsScalars()
        {
            var stats = new FarmStats();
            stats.SeedsNeeded = 5;
            stats.DeadPlants = 3;
            stats.DyingPlants = 2;
            stats.FarmPlotsLowOnWater = 7;
            stats.FarmPlotsReadyToHarvest = 11;
            stats.WaterUsagePerMinute = 4.5f;
            stats.TotalPlantedPlots = 9;
            stats.IsPressurized = true;
            stats.OxygenLevel = 0.8f;
            stats.VentStatusText = "Pressurized";

            stats.Clear();

            stats.SeedsNeeded.Should().Be(0);
            stats.DeadPlants.Should().Be(0);
            stats.DyingPlants.Should().Be(0);
            stats.FarmPlotsLowOnWater.Should().Be(0);
            stats.FarmPlotsReadyToHarvest.Should().Be(0);
            stats.WaterUsagePerMinute.Should().Be(0f);
            stats.TotalPlantedPlots.Should().Be(0);
            stats.IsPressurized.Should().BeFalse();
            stats.OxygenLevel.Should().Be(0f);
            stats.VentStatusText.Should().Be("");
        }

        [Fact]
        public void Clear_EmptiesCollectionsWithoutReallocating()
        {
            var stats = new FarmStats();
            var originalCauses = stats.CausesOfDeath;
            var originalPlotSummary = stats.PlotSummary;
            var originalYieldSummary = stats.YieldSummary;
            var originalGrowthSummary = stats.GrowthSummary;
            var originalAlerts = stats.AlertMessages;

            stats.CausesOfDeath.Add("Dehydration");
            stats.PlotSummary["Potato"] = 4;
            stats.YieldSummary["Potato"] = 12;
            stats.GrowthSummary["Potato"] = 0.5f;
            stats.AlertMessages.Add("Water Low");

            stats.Clear();

            stats.CausesOfDeath.Should().BeEmpty();
            stats.PlotSummary.Should().BeEmpty();
            stats.YieldSummary.Should().BeEmpty();
            stats.GrowthSummary.Should().BeEmpty();
            stats.AlertMessages.Should().BeEmpty();

            // Clearing in place is what makes double buffering allocation free.
            stats.CausesOfDeath.Should().BeSameAs(originalCauses);
            stats.PlotSummary.Should().BeSameAs(originalPlotSummary);
            stats.YieldSummary.Should().BeSameAs(originalYieldSummary);
            stats.GrowthSummary.Should().BeSameAs(originalGrowthSummary);
            stats.AlertMessages.Should().BeSameAs(originalAlerts);
        }
    }
}
