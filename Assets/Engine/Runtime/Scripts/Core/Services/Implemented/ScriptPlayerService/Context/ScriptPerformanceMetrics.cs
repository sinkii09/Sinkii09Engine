using System;
using System.Collections.Generic;
using System.Linq;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Performance metrics for script execution tracking
    /// </summary>
    public class ScriptPerformanceMetrics
    {
        private const int MAX_COMMAND_TIMES = 100;
        private readonly List<float> _commandExecutionTimes = new List<float>();
        private readonly Dictionary<Type, int> _commandCounts = new Dictionary<Type, int>();
        private readonly Dictionary<Type, float> _commandTotalTimes = new Dictionary<Type, float>();
        
        public long StartMemory { get; private set; }
        public long CurrentMemory { get; private set; }
        public long PeakMemory { get; private set; }
        public float TotalGCTime { get; private set; }
        public int GCCount { get; private set; }

        public float AverageCommandTime => _commandExecutionTimes.Count > 0 ? _commandExecutionTimes.Average() : 0f;
        public float PeakCommandTime => _commandExecutionTimes.Count > 0 ? _commandExecutionTimes.Max() : 0f;
        public long MemoryDelta => CurrentMemory - StartMemory;

        public ScriptPerformanceMetrics()
        {
            Reset();
        }

        public void Reset()
        {
            _commandExecutionTimes.Clear();
            _commandCounts.Clear();
            _commandTotalTimes.Clear();
            StartMemory = GC.GetTotalMemory(false);
            CurrentMemory = StartMemory;
            PeakMemory = StartMemory;
            TotalGCTime = 0f;
            GCCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
        }

        public void RecordCommandExecution(Type commandType, float executionTimeSeconds)
        {
            _commandExecutionTimes.Add(executionTimeSeconds);
            if (_commandExecutionTimes.Count > MAX_COMMAND_TIMES)
            {
                _commandExecutionTimes.RemoveAt(0);
            }

            _commandCounts[commandType] = _commandCounts.GetValueOrDefault(commandType) + 1;
            _commandTotalTimes[commandType] = _commandTotalTimes.GetValueOrDefault(commandType) + executionTimeSeconds;
            
            UpdateMemoryMetrics();
        }

        public void RecordGCEvent(float gcTimeSeconds)
        {
            TotalGCTime += gcTimeSeconds;
            var newGCCount = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);
            GCCount = newGCCount - GCCount;
            UpdateMemoryMetrics();
        }

        private void UpdateMemoryMetrics()
        {
            CurrentMemory = GC.GetTotalMemory(false);
            if (CurrentMemory > PeakMemory)
            {
                PeakMemory = CurrentMemory;
            }
        }

        public float GetAverageTimeForCommand(Type commandType)
        {
            if (_commandCounts.TryGetValue(commandType, out var count) && count > 0 &&
                _commandTotalTimes.TryGetValue(commandType, out var totalTime))
            {
                return totalTime / count;
            }
            return 0f;
        }

        public int GetExecutionCountForCommand(Type commandType)
        {
            return _commandCounts.GetValueOrDefault(commandType);
        }
    }
}