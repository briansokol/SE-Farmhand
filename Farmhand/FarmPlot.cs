using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRageMath;

namespace IngameScript
{
    internal class FarmPlot : Block
    {
        /// <summary>
        /// Default light intensity for farm plots
        /// </summary>
        public const float DefaultLightIntensity = 2.0f;

        /// <summary>
        /// Default light radius for farm plots
        /// </summary>
        public const float DefaultLightRadius = 2.5f;

        private readonly IMyFunctionalBlock _farmPlot;
        private readonly IMyFarmPlotLogic _farmPlotLogic;
        private readonly IMyLightingComponent _lightingComponent;
        private readonly IMyResourceStorageComponent _storageComponent;

        protected override IMyFunctionalBlock BlockInstance => _farmPlot;
        protected override Dictionary<string, CustomDataConfig> CustomDataConfigs => null;

        /// <summary>
        /// Initializes a new instance of the FarmPlot class
        /// </summary>
        /// <param name="farmPlot">The Space Engineers farm plot block to wrap</param>
        /// <param name="program">The parent grid program instance</param>
        /// <param name="diagnosticOutput">StringBuilder for diagnostic output</param>
        public FarmPlot(IMyFunctionalBlock farmPlot, MyGridProgram program)
            : base(program)
        {
            _farmPlot = farmPlot;

            foreach (MyComponentBase comp in _farmPlot.Components)
            {
                if (_farmPlotLogic == null)
                {
                    _farmPlotLogic = comp as IMyFarmPlotLogic;
                }

                if (_lightingComponent == null)
                {
                    _lightingComponent = comp as IMyLightingComponent;
                }

                if (_storageComponent == null)
                {
                    _storageComponent = comp as IMyResourceStorageComponent;
                }
            }
        }

        /// <summary>
        /// Gets the custom name of the farm plot block
        /// </summary>
        public string CustomName => IsFunctional() ? _farmPlot.CustomName : string.Empty;

        /// <summary>
        /// Gets whether a plant is currently planted in this farm plot
        /// </summary>
        public bool IsPlantPlanted =>
            IsFunctional() && _farmPlotLogic != null && _farmPlotLogic.IsPlantPlanted;

        /// <summary>
        /// Gets whether the planted crop is alive
        /// </summary>
        public bool IsPlantAlive =>
            IsFunctional() && _farmPlotLogic != null && _farmPlotLogic.IsAlive;

        /// <summary>
        /// Gets whether the planted crop is fully grown and ready for harvest
        /// </summary>
        public bool IsPlantFullyGrown =>
            IsFunctional() && _farmPlotLogic != null && _farmPlotLogic.IsPlantFullyGrown;

        /// <summary>
        /// Gets the type of plant currently plansted in this farm plot
        /// </summary>
        public string PlantType =>
            IsFunctional() && _farmPlotLogic != null && _farmPlotLogic.OutputItem != null
                ? _farmPlotLogic.OutputItem.SubtypeName
                : string.Empty;

        /// <summary>
        /// Gets the amount of yield the current plant will produce when harvested
        /// </summary>
        public int PlantYieldAmount =>
            IsFunctional()
            && _farmPlotLogic != null
            && _farmPlotLogic.IsPlantPlanted
            && _farmPlotLogic.IsAlive
                ? _farmPlotLogic.OutputItemAmount
                : 0;

        /// <summary>
        /// Gets the number of seeds needed to plant in this farm plot
        /// </summary>
        public int SeedsNeeded =>
            IsFunctional()
            && _farmPlotLogic != null
            && (!_farmPlotLogic.IsPlantPlanted || !_farmPlotLogic.IsAlive)
                ? _farmPlotLogic.AmountOfSeedsRequired
                : 0;

        /// <summary>
        /// Gets the current water level in the farm plot storage
        /// </summary>
        public double WaterLevel =>
            IsFunctional() && _storageComponent != null
                ? _storageComponent.FilledRatio * _storageComponent.ResourceCapacity
                : 0f;

        /// <summary>
        /// Gets the maximum water storage capacity of the farm plot
        /// </summary>
        public double WaterCapacity =>
            IsFunctional() && _storageComponent != null ? _storageComponent.ResourceCapacity : 0f;

        /// <summary>
        /// Gets the water fill ratio (0.0 to 1.0) of the farm plot storage
        /// </summary>
        public double WaterFilledRatio =>
            IsFunctional() && _storageComponent != null ? _storageComponent.FilledRatio : 0f;

        /// <summary>
        /// Gets or sets the color of the integrated lighting in the farm plot
        /// </summary>
        public Color LightColor
        {
            get
            {
                return IsFunctional() && _lightingComponent != null
                    ? _lightingComponent.Color
                    : Color.Black;
            }
            set
            {
                if (IsFunctional() && _lightingComponent != null)
                {
                    _lightingComponent.Color = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the blink interval in seconds for the farm plot lighting
        /// </summary>
        public float LightBlinkInterval
        {
            get
            {
                return IsFunctional() && _lightingComponent != null
                    ? _lightingComponent.BlinkIntervalSeconds
                    : 0f;
            }
            set
            {
                if (IsFunctional() && _lightingComponent != null)
                {
                    _lightingComponent.BlinkIntervalSeconds = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the blink length as a ratio (0.0 to 1.0) for the farm plot lighting
        /// </summary>
        public float LightBlinkLength
        {
            get
            {
                return IsFunctional() && _lightingComponent != null
                    ? _lightingComponent.BlinkLength
                    : 0f;
            }
            set
            {
                if (IsFunctional() && _lightingComponent != null)
                {
                    _lightingComponent.BlinkLength = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the intensity of the farm plot lighting
        /// </summary>
        public float LightIntensity
        {
            get
            {
                return IsFunctional() && _lightingComponent != null
                    ? _lightingComponent.Intensity
                    : 0f;
            }
            set
            {
                if (IsFunctional() && _lightingComponent != null)
                {
                    _lightingComponent.Intensity = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the radius of the farm plot lighting
        /// </summary>
        public float LightRadius
        {
            get
            {
                return IsFunctional() && _lightingComponent != null
                    ? _lightingComponent.Radius
                    : 0f;
            }
            set
            {
                if (IsFunctional() && _lightingComponent != null)
                {
                    _lightingComponent.Radius = value;
                }
            }
        }

        /// <summary>
        /// Sets the light color using RGB values
        /// </summary>
        /// <param name="r">Red component (0.0 to 1.0)</param>
        /// <param name="g">Green component (0.0 to 1.0)</param>
        /// <param name="b">Blue component (0.0 to 1.0)</param>
        public void SetLightColor(float r, float g, float b)
        {
            if (IsFunctional() && _lightingComponent != null)
            {
                _lightingComponent.Color = new Color(r, g, b);
            }
        }

        /// <summary>
        /// Sets the light color using a Color object
        /// </summary>
        /// <param name="color">The color to set for the farm plot lighting</param>
        public void SetLightColor(Color color)
        {
            if (IsFunctional() && _lightingComponent != null)
            {
                _lightingComponent.Color = color;
            }
        }

        /// <summary>
        /// Validates whether a block can be used as a farm plot
        /// </summary>
        /// <param name="block">The block to validate</param>
        /// <returns>True if the block can be used as a farm plot</returns>
        public static bool BlockIsValid(IMyFunctionalBlock block)
        {
            if (!IsBlockValid(block))
            {
                return false;
            }

            foreach (MyComponentBase comp in block.Components)
            {
                var farmPlotLogic = comp as IMyFarmPlotLogic;
                if (farmPlotLogic != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
