using System.Collections.Generic;

namespace IngameScript
{
    /// <summary>
    /// Contains all statistical data collected from farm operations
    /// </summary>
    internal class FarmStats
    {
        /// <summary>
        /// Number of empty plots needing seeds
        /// </summary>
        public int SeedsNeeded { get; set; }

        /// <summary>
        /// Number of dead plants
        /// </summary>
        public int DeadPlants { get; set; }

        /// <summary>
        /// Number of plants with low health (dying)
        /// </summary>
        public int DyingPlants { get; set; }

        /// <summary>
        /// Number of farm plots with low water levels
        /// </summary>
        public int FarmPlotsLowOnWater { get; set; }

        /// <summary>
        /// Number of farm plots ready to harvest
        /// </summary>
        public int FarmPlotsReadyToHarvest { get; set; }

        /// <summary>
        /// Total water usage per minute across all plots
        /// </summary>
        public float WaterUsagePerMinute { get; set; }

        /// <summary>
        /// List of causes of death for dead plants
        /// </summary>
        public List<string> CausesOfDeath { get; set; }

        /// <summary>
        /// Dictionary mapping plant type to number of plots with that plant
        /// </summary>
        public Dictionary<string, int> PlotSummary { get; set; }

        /// <summary>
        /// Dictionary mapping plant type to total yield available for harvest
        /// </summary>
        public Dictionary<string, int> YieldSummary { get; set; }

        /// <summary>
        /// Dictionary mapping plant type to growth progress (0.0 to 1.0)
        /// </summary>
        public Dictionary<string, float> GrowthSummary { get; set; }

        /// <summary>
        /// Total number of planted plots (alive plants)
        /// </summary>
        public int TotalPlantedPlots { get; set; }

        /// <summary>
        /// Whether the atmosphere is pressurized
        /// </summary>
        public bool IsPressurized { get; set; }

        /// <summary>
        /// Oxygen level as a ratio (0.0 to 1.0)
        /// </summary>
        public float OxygenLevel { get; set; }

        /// <summary>
        /// Status text for the air vent
        /// </summary>
        public string VentStatusText { get; set; }

        /// <summary>
        /// Ice volume ratio in irrigation systems (0.0 to 1.0)
        /// </summary>
        public float IceRatio { get; set; }

        /// <summary>
        /// Current mass of ice in kilograms
        /// </summary>
        public float CurrentIceKg { get; set; }

        /// <summary>
        /// Max mass of ice in kilograms
        /// </summary>
        public float MaxIceKg { get; set; }

        /// <summary>
        /// List of alert messages requiring attention
        /// </summary>
        public List<string> AlertMessages { get; set; }

        /// <summary>
        /// Initializes a new instance of the FarmStats class with empty collections
        /// </summary>
        public FarmStats()
        {
            CausesOfDeath = new List<string>();
            PlotSummary = new Dictionary<string, int>();
            YieldSummary = new Dictionary<string, int>();
            GrowthSummary = new Dictionary<string, float>();
            AlertMessages = new List<string>();

            SeedsNeeded = 0;
            DeadPlants = 0;
            DyingPlants = 0;
            FarmPlotsLowOnWater = 0;
            FarmPlotsReadyToHarvest = 0;
            WaterUsagePerMinute = 0f;
            TotalPlantedPlots = 0;
            IsPressurized = false;
            OxygenLevel = 0f;
            VentStatusText = "";
            IceRatio = 0f;
        }
    }
}
