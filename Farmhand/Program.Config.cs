using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    public partial class Program
    {
        string _lastSeenCustomData;
        bool _configDirty = true;

        /// <summary>
        /// Reparses programmable block configuration only when its CustomData string
        /// actually changed, which also detects manual player edits with no explicit
        /// invalidation call.
        /// </summary>
        IEnumerator<YieldReason> StepParseConfigIfDirty()
        {
            if (Me.CustomData != _lastSeenCustomData)
            {
                _configDirty = true;
            }

            if (!_configDirty)
            {
                yield return YieldReason.ChunkBoundary;
                yield break;
            }

            _lastSeenCustomData = Me.CustomData;
            thisPb.ParseCustomData();

            // The accessors clamp; this step only publishes the values onto Program.
            BudgetFraction = thisPb.BudgetFraction;
            RescanIntervalCycles = thisPb.RescanIntervalCycles;
            DebugLogging = thisPb.DebugLogging;

            _configDirty = false;

            yield return YieldReason.ChunkBoundary;
        }
    }
}
