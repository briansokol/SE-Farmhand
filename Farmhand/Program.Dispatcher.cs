using System;
using System.Collections.Generic;

namespace IngameScript
{
    public partial class Program
    {
        /// <summary>Why a pipeline step yielded back to the dispatcher.</summary>
        public enum YieldReason
        {
            /// <summary>Per-run instruction budget was hit; resume next tick.</summary>
            BudgetHit,

            /// <summary>Logical chunk completed; resume next tick.</summary>
            ChunkBoundary
        }

        /// <summary>Fraction of the per-run instruction allowance a single run may consume.</summary>
        public float BudgetFraction = 0.8f;

        /// <summary>Whether to show verbose pipeline detail on the programmable block screen.</summary>
        public bool DebugLogging;

        /// <summary>True while the pipeline is paused via the "pause" command.</summary>
        bool _paused;

        /// <summary>Index of the currently executing step.</summary>
        int _stepIndex;

        /// <summary>Human readable label of the currently executing step.</summary>
        string _stepLabel = "init";

        /// <summary>Ticks consumed by the cycle currently in progress.</summary>
        public int TicksThisCycle { get; private set; }

        /// <summary>Ticks consumed by the last completed cycle.</summary>
        public int TicksLastCycle { get; private set; }

        /// <summary>Highest instruction count observed during the current cycle.</summary>
        public int InstructionHighWater { get; private set; }

        /// <summary>
        /// Cost of the most expensive single uninterruptible chunk in the current cycle.
        /// The peak per run is the budget guard plus this, so this is the number that
        /// decides how much headroom BudgetFraction has to leave.
        /// </summary>
        public int WorstChunkCost { get; private set; }

        /// <summary>Step the worst chunk of the current cycle belonged to.</summary>
        public string WorstChunkStep { get; private set; }

        static readonly string[] StepLabels =
        {
            "Discovery", "Config", "ScanPlots", "ScanSupport", "Commit",
            "BuildText", "RenderText", "BuildSprites", "FlushSprites", "RenderPlotLCDs"
        };

        IEnumerator<YieldReason> _workIterator;

        /// <summary>True when this run has consumed its share of the instruction allowance.</summary>
        bool BudgetExceeded()
        {
            return Runtime.CurrentInstructionCount >
                   Runtime.MaxInstructionCount * BudgetFraction;
        }

        /// <summary>
        /// Pumps the work iterator repeatedly within a single tick until the budget trips
        /// or the iterator finishes. Step faults are handled inside StepRoot; the catch here
        /// is only a last-resort backstop for anything thrown outside a step.
        /// </summary>
        void RunOneTick()
        {
            TicksThisCycle++;
            try
            {
                do
                {
                    int before = Runtime.CurrentInstructionCount;
                    if (_workIterator == null || !_workIterator.MoveNext())
                    {
                        _workIterator = StepRoot();
                        break;
                    }
                    // The budget is checked after a chunk runs, never before, so a chunk
                    // cannot be interrupted once entered. Recording the worst one names the
                    // unit that has to be split if the peak ever threatens the hard ceiling.
                    // _stepLabel is read after MoveNext returns, so it names the step that
                    // just yielded rather than the one about to start.
                    int cost = Runtime.CurrentInstructionCount - before;
                    if (cost > WorstChunkCost)
                    {
                        WorstChunkCost = cost;
                        WorstChunkStep = _stepLabel;
                    }
                } while (!BudgetExceeded());
            }
            catch (Exception ex)
            {
                LogStepError(ex);
                _workIterator = StepRoot();
            }

            if (Runtime.CurrentInstructionCount > InstructionHighWater)
            {
                InstructionHighWater = Runtime.CurrentInstructionCount;
            }
        }

        /// <summary>Top level work loop iterating over each pipeline step indefinitely.</summary>
        IEnumerator<YieldReason> StepRoot()
        {
            while (true)
            {
                // CycleNumber gates every cache in the script, so it must advance here at
                // the top of each pass. StepRoot never terminates, which makes the
                // !MoveNext() branch in RunOneTick purely defensive.
                CycleNumber++;
                shiftSprites = !shiftSprites;
                Block.CurrentCycle = CycleNumber;
                ApplyFrameState();
                RefreshGroupSnapshot();
                TicksLastCycle = TicksThisCycle;
                TicksThisCycle = 0;
                InstructionHighWater = 0;
                WorstChunkCost = 0;

                // Named so the worst-chunk diagnostic attributes the preamble to itself
                // rather than to whichever step happened to run last.
                _stepLabel = "Preamble";
                _stepIndex = -1;

                // Split the preamble off from step 0. Without this, the wrap-around from the
                // last step of the previous cycle runs the preamble AND all of
                // StepDiscoveryIfDue's first section, including the full-grid
                // FindFarmLCDBlocks, in one uninterruptible MoveNext on top of whatever the
                // tick had already spent. That is the largest unyielded unit in the script.
                yield return YieldReason.ChunkBoundary;

                for (int i = 0; i < StepLabels.Length; i++)
                {
                    _stepIndex = i;
                    _stepLabel = StepLabels[i];
                    IEnumerator<YieldReason> step = StepFor(i);
                    // Explicit MoveNext pump rather than foreach, so a faulting step can be
                    // abandoned without abandoning the cycle. C# forbids yield inside a try
                    // that has a catch, so only the MoveNext call sits inside the try.
                    while (true)
                    {
                        bool moved;
                        try
                        {
                            moved = step.MoveNext();
                        }
                        catch (Exception ex)
                        {
                            // Skip the rest of this step and continue the cycle, matching the
                            // pre-2.0 handler. Restarting the whole pipeline here would mean a
                            // reproducible fault in an early step permanently starves every
                            // later step, freezing displays with no visible cause.
                            LogStepError(ex);
                            break;
                        }
                        if (!moved)
                        {
                            break;
                        }
                        yield return step.Current;
                    }
                    yield return YieldReason.ChunkBoundary;
                }
            }
        }

        /// <summary>Records a step fault on the programmable block screen.</summary>
        void LogStepError(Exception ex)
        {
            Echo($"Error in step {_stepIndex} {_stepLabel}: {ex.Message}");
        }

        /// <summary>Returns the iterator for the step at the given index.</summary>
        IEnumerator<YieldReason> StepFor(int i)
        {
            switch (i)
            {
                case 0: return StepDiscoveryIfDue();
                case 1: return StepParseConfigIfDirty();
                case 2: return StepScanPlots();
                case 3: return StepScanSupport();
                case 4: return StepCommitSnapshot();
                case 5: return StepBuildTextOutput();
                case 6: return StepRenderText();
                case 7: return StepBuildGraphicalSprites();
                case 8: return StepFlushGraphical();
                case 9: return StepRenderPlotLCDs();
                default: return NoOpStep();
            }
        }

        /// <summary>
        /// Defensive no-op iterator used when the step index drifts out of range, so an
        /// off-by-one bug degrades to a wasted tick rather than an exception.
        /// </summary>
        IEnumerator<YieldReason> NoOpStep()
        {
            yield return YieldReason.ChunkBoundary;
        }
    }
}
