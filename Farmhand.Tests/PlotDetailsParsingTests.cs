using System;
using FluentAssertions;
using IngameScript;
using Xunit;

namespace Farmhand.Tests
{
    /// <summary>
    /// Tests for the detailed-info parser extracted from FarmPlot. The parser identifies
    /// values by format rather than by key name so it works in non-English game languages.
    /// </summary>
    public class PlotDetailsParsingTests
    {
        const string EnglishGrowing =
            "Type: Farm Plot\n" +
            "Growth Progress: 45.5 %\n" +
            "Grow Time: 00:30:00\n" +
            "Crop Health: 87.3 %\n" +
            "Current Water Usage: 1.6 L/min\n";

        [Fact]
        public void Parse_NullInput_ReturnsNull()
        {
            FarmPlot.ParsePlotDetails(null).Should().BeNull();
        }

        [Fact]
        public void Parse_EmptyInput_ReturnsNull()
        {
            FarmPlot.ParsePlotDetails("").Should().BeNull();
        }

        [Fact]
        public void Parse_WhitespaceInput_ReturnsNull()
        {
            FarmPlot.ParsePlotDetails("   \n  \n").Should().BeNull();
        }

        [Fact]
        public void Parse_EnglishGrowingPlot_ReadsAllFields()
        {
            var details = FarmPlot.ParsePlotDetails(EnglishGrowing);

            details.Should().NotBeNull();
            details.GrowthProgress.Should().BeApproximately(0.455f, 0.0001f);
            details.CropHealth.Should().BeApproximately(0.873f, 0.0001f);
            details.GrowTime.Should().Be(TimeSpan.FromMinutes(30));
            details.WaterUsage.Should().BeApproximately(1.6f, 0.0001f);
        }

        [Fact]
        public void Parse_PercentagesAreOrdinal_GrowthFirstThenHealth()
        {
            // The parser deliberately ignores key names and takes percentages in order
            // of appearance, which is what makes non-English clients work.
            var details = FarmPlot.ParsePlotDetails(
                "Fortschritt: 10.0 %\n" +
                "Gesundheit: 90.0 %\n");

            details.GrowthProgress.Should().BeApproximately(0.10f, 0.0001f);
            details.CropHealth.Should().BeApproximately(0.90f, 0.0001f);
        }

        [Fact]
        public void Parse_NonEnglishNumberFormatting_UsesInvariantCulture()
        {
            // Values arrive with '.' as the decimal separator regardless of client locale.
            var details = FarmPlot.ParsePlotDetails("Progress: 7.5 %\n");

            details.GrowthProgress.Should().BeApproximately(0.075f, 0.0001f);
        }

        [Fact]
        public void Parse_StripsColorFormattingTags()
        {
            var details = FarmPlot.ParsePlotDetails(
                "[Color=#FFFF0000]Crop Health: 12.5 %[/Color]\n");

            // Only one percentage present, so it lands in GrowthProgress by ordinal rule.
            details.GrowthProgress.Should().BeApproximately(0.125f, 0.0001f);
        }

        [Fact]
        public void Parse_CauseOfDeath_ReadsSingleWordValue()
        {
            var details = FarmPlot.ParsePlotDetails("Cause of Death: Dehydration\n");

            details.CauseOfDeath.Should().Be("Dehydration");
        }

        [Fact]
        public void Parse_MissingFields_LeavesDefaults()
        {
            var details = FarmPlot.ParsePlotDetails("Type: Farm Plot\n");

            details.Should().NotBeNull();
            details.GrowthProgress.Should().Be(0f);
            details.CropHealth.Should().Be(0f);
            details.GrowTime.Should().Be(TimeSpan.Zero);
            details.WaterUsage.Should().Be(0f);
            details.CauseOfDeath.Should().Be(string.Empty);
        }

        [Fact]
        public void Parse_MalformedPercentage_IsIgnored()
        {
            var details = FarmPlot.ParsePlotDetails("Growth Progress: abc %\n");

            details.Should().NotBeNull();
            details.GrowthProgress.Should().Be(0f);
        }

        [Fact]
        public void Parse_LinesWithoutColon_AreSkipped()
        {
            var details = FarmPlot.ParsePlotDetails(
                "Some header line with no separator\n" +
                "Growth Progress: 33.0 %\n");

            details.GrowthProgress.Should().BeApproximately(0.33f, 0.0001f);
        }
    }
}
