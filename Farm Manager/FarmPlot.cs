using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRageMath;

namespace IngameScript
{
    internal class FarmPlot : Block
    {
        private readonly StringBuilder _diagnosticOutput;
        private readonly IMyFunctionalBlock _farmPlot;
        private readonly IMyFarmPlotLogic _farmPlotLogic;
        private readonly IMyLightingComponent _lightingComponent;
        private readonly IMyResourceStorageComponent _storageComponent;

        private readonly Dictionary<string, string> _customDataEntries =
            new Dictionary<string, string>();

        protected override IMyFunctionalBlock BlockInstance => _farmPlot;
        protected override Dictionary<string, string> CustomDataEntries => _customDataEntries;

        public FarmPlot(
            IMyFunctionalBlock farmPlot,
            MyGridProgram program,
            StringBuilder diagnosticOutput
        )
            : base(program)
        {
            _farmPlot = farmPlot;
            _diagnosticOutput = diagnosticOutput;

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

            if (_farmPlotLogic == null)
            {
                _diagnosticOutput.AppendLine(
                    $"ERROR: Farm Plot '{_farmPlot.CustomName}' is missing IMyFarmPlotLogic component"
                );
            }
        }

        public string CustomName => IsFunctional() ? _farmPlot.CustomName : string.Empty;

        public bool IsPlantPlanted =>
            IsFunctional() && _farmPlotLogic != null && _farmPlotLogic.IsPlantPlanted;

        public bool IsPlantAlive =>
            IsFunctional() && _farmPlotLogic != null && _farmPlotLogic.IsAlive;

        public bool IsPlantFullyGrown =>
            IsFunctional() && _farmPlotLogic != null && _farmPlotLogic.IsPlantFullyGrown;

        public string PlantType =>
            IsFunctional() && _farmPlotLogic != null
                ? _farmPlotLogic.OutputItem.SubtypeName
                : string.Empty;

        public int PlantYieldAmount =>
            IsFunctional() && _farmPlotLogic != null && IsPlantPlanted && IsPlantAlive
                ? _farmPlotLogic.OutputItemAmount
                : 0;

        public int SeedsNeeded =>
            IsFunctional() && _farmPlotLogic != null && (!IsPlantPlanted || !IsPlantAlive)
                ? _farmPlotLogic.AmountOfSeedsRequired
                : 0;

        public double WaterLevel =>
            IsFunctional() && _storageComponent != null
                ? _storageComponent.FilledRatio * _storageComponent.ResourceCapacity
                : 0f;

        public double WaterCapacity =>
            IsFunctional() && _storageComponent != null ? _storageComponent.ResourceCapacity : 0f;

        public double WaterFilledRatio =>
            IsFunctional() && _storageComponent != null ? _storageComponent.FilledRatio : 0f;

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

        public void SetLightColor(float r, float g, float b)
        {
            if (IsFunctional() && _lightingComponent != null)
            {
                _lightingComponent.Color = new Color(r, g, b);
            }
        }

        public void SetLightColor(Color color)
        {
            if (IsFunctional() && _lightingComponent != null)
            {
                _lightingComponent.Color = color;
            }
        }

        public static bool BlockIsValid(IMyFunctionalBlock block)
        {
            if (!IsValid(block))
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
