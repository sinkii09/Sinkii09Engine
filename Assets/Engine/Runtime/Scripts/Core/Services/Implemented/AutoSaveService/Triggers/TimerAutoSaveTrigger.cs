using Sinkii09.Engine.Services.AutoSave;
using UnityEngine;

namespace Sinkii09.Engine.Core.Services.AutoSave
{
    public class TimerAutoSaveTrigger : AutoSaveTriggerBase
    {
        public override string TriggerName => "Timer";
        
        private float _lastTriggerTime;
        private float _interval;
        private float _minimumInterval;
        
        protected override void OnInitialize()
        {
            if (_config?.TimerSettings?.Enabled == true)
            {
                _interval = _config.TimerSettings.Interval;
                _minimumInterval = _config.TimerSettings.MinimumInterval;
                _lastTriggerTime = Time.time;
                IsEnabled = true;
                
                Debug.Log($"[TimerAutoSaveTrigger] Initialized with interval: {_interval}s, minimum: {_minimumInterval}s");
            }
            else
            {
                IsEnabled = false;
                Debug.Log("[TimerAutoSaveTrigger] Disabled via configuration");
            }
        }
        
        protected override void OnUpdate(float deltaTime)
        {
            var currentTime = Time.time;
            var timeSinceLastTrigger = currentTime - _lastTriggerTime;
            
            if (timeSinceLastTrigger >= _interval)
            {
                // Ensure we don't trigger too frequently
                if (timeSinceLastTrigger >= _minimumInterval)
                {
                    _lastTriggerTime = currentTime;
                    TriggerAutoSave(AutoSaveReason.Timer);
                }
            }
        }
        
        protected override void OnShutdown()
        {
            Debug.Log("[TimerAutoSaveTrigger] Shutdown");
        }
        
        public void ResetTimer()
        {
            _lastTriggerTime = Time.time;
        }
        
        public float GetTimeUntilNextSave()
        {
            var timeSinceLastTrigger = Time.time - _lastTriggerTime;
            return Mathf.Max(0, _interval - timeSinceLastTrigger);
        }
    }
}