using FluentAssertions;
using IngameScript;
using Xunit;

namespace Farmhand.Tests
{
    /// <summary>
    /// Tests for plot light role and blink selection. Reproduces the two-pass ordering
    /// of the original inline logic: plant state selects a role, then water state can
    /// override both role and blink.
    /// </summary>
    public class PlotLightStateTests
    {
        const float HealthLow = 0.30f;
        const float WaterLow = 0.20f;

        static PlotLightInputs Inputs(
            bool planted, bool alive, bool fullyGrown,
            float health, float water,
            bool functional = true, bool hasDetails = true)
        {
            var i = new PlotLightInputs();
            i.IsFunctional = functional;
            i.IsPlanted = planted;
            i.IsAlive = alive;
            i.IsFullyGrown = fullyGrown;
            i.HasDetails = hasDetails;
            i.CropHealth = health;
            i.WaterFilledRatio = water;
            i.HealthLowThreshold = HealthLow;
            i.WaterLowThreshold = WaterLow;
            return i;
        }

        [Fact]
        public void EmptyPlot_WithWater_IsEmptyAndNotBlinking()
        {
            var d = FarmPlot.DecidePlotLight(Inputs(false, false, false, 0f, 0.9f));

            d.Role.Should().Be(PlotLightRole.Empty);
            d.BlinkInterval.Should().Be(0f);
            d.BlinkLength.Should().Be(1f);
        }

        [Fact]
        public void GrowingHealthyPlot_WithWater_IsGrowingAndNotBlinking()
        {
            var d = FarmPlot.DecidePlotLight(Inputs(true, true, false, 0.9f, 0.9f));

            d.Role.Should().Be(PlotLightRole.Growing);
            d.BlinkInterval.Should().Be(0f);
        }

        [Fact]
        public void FullyGrownPlot_IsReadyAndNotBlinking()
        {
            var d = FarmPlot.DecidePlotLight(Inputs(true, true, true, 0.9f, 0.9f));

            d.Role.Should().Be(PlotLightRole.Ready);
            d.BlinkInterval.Should().Be(0f);
            d.BlinkLength.Should().Be(1f);
        }

        [Fact]
        public void FullyGrownPlot_BeatsLowHealth()
        {
            // Ready to harvest wins over the low-health warning.
            var d = FarmPlot.DecidePlotLight(Inputs(true, true, true, 0.05f, 0.9f));

            d.Role.Should().Be(PlotLightRole.Ready);
        }

        [Fact]
        public void FullyGrownPlot_BeatsLowWater()
        {
            var d = FarmPlot.DecidePlotLight(Inputs(true, true, true, 0.9f, 0.01f));

            d.Role.Should().Be(PlotLightRole.Ready);
            d.BlinkInterval.Should().Be(0f);
        }

        [Fact]
        public void LowHealthPlot_IsDeadColourAndBlinking()
        {
            var d = FarmPlot.DecidePlotLight(Inputs(true, true, false, 0.05f, 0.9f));

            d.Role.Should().Be(PlotLightRole.Dead);
            d.BlinkInterval.Should().Be(2f);
            d.BlinkLength.Should().Be(50f);
        }

        [Fact]
        public void LowHealthPlot_BeatsLowWater_AndKeepsBlinking()
        {
            var d = FarmPlot.DecidePlotLight(Inputs(true, true, false, 0.05f, 0.01f));

            d.Role.Should().Be(PlotLightRole.Dead);
            d.BlinkInterval.Should().Be(2f);
            d.BlinkLength.Should().Be(50f);
        }

        [Fact]
        public void LowHealth_WithoutDetails_IsNotTreatedAsLowHealth()
        {
            // HasDetails false means CropHealth is not trustworthy.
            var d = FarmPlot.DecidePlotLight(
                Inputs(true, true, false, 0.05f, 0.9f, hasDetails: false));

            d.Role.Should().Be(PlotLightRole.Growing);
            d.BlinkInterval.Should().Be(0f);
        }

        [Fact]
        public void DeadPlant_IsDeadAndNotBlinking()
        {
            var d = FarmPlot.DecidePlotLight(Inputs(true, false, false, 0f, 0.9f));

            d.Role.Should().Be(PlotLightRole.Dead);
            d.BlinkInterval.Should().Be(0f);
        }

        [Fact]
        public void GrowingPlot_WithLowWater_IsWaterLowAndBlinking()
        {
            var d = FarmPlot.DecidePlotLight(Inputs(true, true, false, 0.9f, 0.01f));

            d.Role.Should().Be(PlotLightRole.WaterLow);
            d.BlinkInterval.Should().Be(2f);
            d.BlinkLength.Should().Be(50f);
        }

        [Fact]
        public void EmptyPlot_WithLowWater_IsWaterLow()
        {
            var d = FarmPlot.DecidePlotLight(Inputs(false, false, false, 0f, 0.01f));

            d.Role.Should().Be(PlotLightRole.WaterLow);
            d.BlinkInterval.Should().Be(2f);
        }

        [Fact]
        public void NonFunctionalPlot_NeverReportsWaterLow()
        {
            var d = FarmPlot.DecidePlotLight(
                Inputs(true, true, false, 0.9f, 0.01f, functional: false));

            d.Role.Should().Be(PlotLightRole.Growing);
            d.BlinkInterval.Should().Be(0f);
        }

        [Fact]
        public void WaterExactlyAtThreshold_CountsAsLow()
        {
            // Original uses <= for the water comparison.
            var d = FarmPlot.DecidePlotLight(Inputs(true, true, false, 0.9f, WaterLow));

            d.Role.Should().Be(PlotLightRole.WaterLow);
        }

        [Fact]
        public void HealthExactlyAtThreshold_IsNotLow()
        {
            // Original uses < for the health comparison.
            var d = FarmPlot.DecidePlotLight(Inputs(true, true, false, HealthLow, 0.9f));

            d.Role.Should().Be(PlotLightRole.Growing);
        }
    }
}
