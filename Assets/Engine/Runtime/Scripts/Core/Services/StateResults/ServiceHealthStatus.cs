using System;
using System.Collections.Generic;

namespace Sinkii09.Engine.Services
{
    /// <summary>
    /// Health status of a service
    /// </summary>
    public class ServiceHealthStatus
    {
        public bool IsHealthy { get; set; }
        public string StatusMessage { get; set; }
        public DateTime LastCheckTime { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public Dictionary<string, object> Details { get; set; }
        
        public ServiceHealthStatus()
        {
            LastCheckTime = DateTime.UtcNow;
            Details = new Dictionary<string, object>();
        }
        
        /// <summary>
        /// Create a healthy status
        /// </summary>
        public static ServiceHealthStatus Healthy(string message = "Service is healthy", TimeSpan? responseTime = null)
        {
            return new ServiceHealthStatus
            {
                IsHealthy = true,
                StatusMessage = message,
                ResponseTime = responseTime ?? TimeSpan.Zero,
                LastCheckTime = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// Create an unhealthy status
        /// </summary>
        public static ServiceHealthStatus Unhealthy(string message = "Service is unhealthy", TimeSpan? responseTime = null)
        {
            return new ServiceHealthStatus
            {
                IsHealthy = false,
                StatusMessage = message,
                ResponseTime = responseTime ?? TimeSpan.Zero,
                LastCheckTime = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// Create an unknown status
        /// </summary>
        public static ServiceHealthStatus Unknown(string message = "Service health is unknown")
        {
            return new ServiceHealthStatus
            {
                IsHealthy = false,
                StatusMessage = message,
                LastCheckTime = DateTime.UtcNow
            };
        }
        
        /// <summary>
        /// Add detail information to the health status
        /// </summary>
        public ServiceHealthStatus WithDetail(string key, object value)
        {
            Details[key] = value;
            return this;
        }
        
        public override string ToString()
        {
            var status = IsHealthy ? "Healthy" : "Unhealthy";
            var time = ResponseTime.TotalMilliseconds > 0 ? $" ({ResponseTime.TotalMilliseconds:F2}ms)" : "";
            return $"{status}: {StatusMessage}{time}";
        }
    }
}