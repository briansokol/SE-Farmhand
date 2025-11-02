using System.Collections.Generic;

namespace IngameScript
{
    /// <summary>
    /// Manages state tracking for timer events and triggers when states change
    /// </summary>
    internal class StateManager
    {
        private readonly Dictionary<string, bool> _previousStates = new Dictionary<string, bool>();
        private readonly List<Timer> _timers = new List<Timer>();
        private readonly List<ActionRelay> _actionRelays = new List<ActionRelay>();

        /// <summary>
        /// Registers a timer to be managed by this state manager
        /// </summary>
        /// <param name="timer">The timer to register</param>
        public void RegisterTimer(Timer timer)
        {
            _timers.Add(timer);
        }

        /// <summary>
        /// Registers an action relay to be managed by this state manager
        /// </summary>
        /// <param name="actionRelay">The action relay to register</param>
        public void RegisterActionRelay(ActionRelay actionRelay)
        {
            _actionRelays.Add(actionRelay);
        }

        /// <summary>
        /// Clears all registered timers
        /// </summary>
        public void ClearTimers()
        {
            _timers.Clear();
        }

        /// <summary>
        /// Clears all registered action relays
        /// </summary>
        public void ClearActionRelays()
        {
            _actionRelays.Clear();
        }

        /// <summary>
        /// Updates a state value and triggers associated timers and action relays if the state changed
        /// </summary>
        /// <param name="stateName">The name of the state to update</param>
        /// <param name="currentValue">The current value of the state</param>
        public void UpdateState(string stateName, bool currentValue)
        {
            bool previousValue = _previousStates.ContainsKey(stateName)
                ? _previousStates[stateName]
                : currentValue;

            // Only process if the state has changed
            if (previousValue != currentValue)
            {
                _previousStates[stateName] = currentValue;

                // Determine which event to trigger based on the state change
                string eventToTrigger = GetEventName(stateName, currentValue);

                // Trigger all registered timers for this event
                foreach (Timer timer in _timers)
                {
                    timer.Trigger(eventToTrigger);
                }

                // Trigger all registered action relays for this event
                foreach (ActionRelay actionRelay in _actionRelays)
                {
                    actionRelay.Trigger(eventToTrigger);
                }
            }
            else
            {
                // Update the stored state even if it hasn't changed
                _previousStates[stateName] = currentValue;
            }
        }

        /// <summary>
        /// Gets the appropriate event name based on state name and value
        /// </summary>
        /// <param name="stateName">The base state name</param>
        /// <param name="value">The current boolean value</param>
        /// <returns>The event name to trigger</returns>
        private string GetEventName(string stateName, bool value)
        {
            return $"{stateName}{(value ? "True" : "False")}";
        }

        /// <summary>
        /// Checks if a state has changed since the last update
        /// </summary>
        /// <param name="stateName">The name of the state to check</param>
        /// <param name="currentValue">The current value to compare against</param>
        /// <returns>True if the state has changed</returns>
        public bool HasStateChanged(string stateName, bool currentValue)
        {
            bool previousValue = _previousStates.ContainsKey(stateName)
                ? _previousStates[stateName]
                : !currentValue;
            return previousValue != currentValue;
        }

        /// <summary>
        /// Gets the previous value of a state
        /// </summary>
        /// <param name="stateName">The name of the state</param>
        /// <returns>The previous value, or null if no previous value exists</returns>
        public bool? GetPreviousState(string stateName)
        {
            return _previousStates.ContainsKey(stateName)
                ? (bool?)_previousStates[stateName]
                : null;
        }

        /// <summary>
        /// Resets all tracked states
        /// </summary>
        public void ResetStates()
        {
            _previousStates.Clear();
        }

        /// <summary>
        /// Gets the count of currently tracked states
        /// </summary>
        public int TrackedStateCount => _previousStates.Count;

        /// <summary>
        /// Gets the count of registered timers
        /// </summary>
        public int RegisteredTimerCount => _timers.Count;

        /// <summary>
        /// Gets the count of registered action relays
        /// </summary>
        public int RegisteredActionRelayCount => _actionRelays.Count;
    }
}
