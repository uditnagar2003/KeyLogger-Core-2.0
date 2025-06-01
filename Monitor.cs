using System.Diagnostics;
namespace VisualKeyloggerDetector.Core.Monitoring
{
    
    public class MonitoringResult : Dictionary<uint, ulong> { }

   
    public class Monitors
    {
        private readonly ExperimentConfiguration _config;

     
        public event EventHandler<string> StatusUpdate;

      
        public event EventHandler<int> ProgressUpdate;

       
        protected virtual void OnStatusUpdate(string message) => StatusUpdate?.Invoke(this, message);

       
        protected virtual void OnProgressUpdate(int intervalIndex) => ProgressUpdate?.Invoke(this, intervalIndex);
        public MonitoringResult results;
        public MonitoringResult lastWriteCounts;
       
        public Monitors(ExperimentConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            // OnStatusUpdate($"Starting monitoring for {processIdList.Count} process(es)...");
            results = new MonitoringResult();
            // Stores the last known WriteTransferCount for each process
            lastWriteCounts = new MonitoringResult();


        }

        
        public async Task<MonitoringResult> MonitorProcessesAsync(IEnumerable<uint> processIdsToMonitor, CancellationToken cancellationToken = default)
        {
            var processIdList = processIdsToMonitor?.ToList() ?? new List<uint>();
            if (!processIdList.Any())
            {
                OnStatusUpdate("No processes specified for monitoring.");
                //return new MonitoringResult();
            }
                      
            var processSet = new HashSet<uint>(processIdList);
          
            var stopwatch = new Stopwatch();
           

            // --- Interval Monitoring Loop ---
            OnStatusUpdate("Starting interval monitoring...");
            //for (int i = 0; i < _config.PatternLengthN; i++)
            {
                // Check for cancellation at the start of each interval
                cancellationToken.ThrowIfCancellationRequested();

                stopwatch.Restart();

                // Wait for the interval duration. We query *after* the interval.
                // await Task.Delay(intervalDuration, cancellationToken);

                // --- Query Process Info Again ---
                Dictionary<uint, ulong> currentWriteCounts = new Dictionary<uint, ulong>();
                try
                {
                    var currentProcessInfo = await ProcessMonitor.GetAllProcessesInfoAsync();
                    foreach (var pInfo in currentProcessInfo)
                    {
                        // Only store counts for processes we are actively monitoring
                        if (processSet.Contains(pInfo.Id))
                        {
                            currentWriteCounts[pInfo.Id] = pInfo.WriteCount;
                        }
                    }
                }
                catch (Exception ex)
                {
                    OnStatusUpdate($"Warning: Error querying processes in interval : {ex.Message}. Results for this interval may be incomplete.");
                    // Continue with potentially empty currentWriteCounts
                }


                // --- Calculate Bytes Written During This Interval ---
                foreach (uint pid in processSet)
                {
                    ulong bytesWrittenThisInterval = 0; // Default to 0

                    // Try to get current and last counts for the process
                    bool currentFound = currentWriteCounts.TryGetValue(pid, out ulong currentCount);
                    bool lastFound = lastWriteCounts.TryGetValue(pid, out ulong lastCount);

                    if (currentFound)
                    {
                        if (lastFound)
                        {
                            // Handle counter wrap-around (very unlikely for ulong) or process restart
                            if (currentCount >= lastCount)
                            {
                                bytesWrittenThisInterval = currentCount - lastCount;
                                results[pid] = bytesWrittenThisInterval;
                            }
                            // else: Process might have restarted, or counter wrapped. Treat as 0 write for this interval.
                        }
                        else
                        {
                            // Process appeared during monitoring (no baseline). Treat first interval's write as 0 diff or use full count?
                            // Let's treat as 0 diff for consistency, assuming baseline wasn't captured.
                            // bytesWrittenThisInterval = currentCount; // Alternative: use full count if no baseline
                        }
                        // Update last count for the next interval
                        }
                    else
                    {
                        // Process disappeared or wasn't found in the current query.
                        lastWriteCounts.Remove(pid); // Stop tracking baseline for this PID
                    }

                   
                } 
                stopwatch.Stop(); // Optional: Log if interval took longer than expected due to WMI query time

              
            } // End interval loop (i)

           
            OnStatusUpdate("Monitoring finished fir this interal ");
            Debug.WriteLine($"Monitoring finished. {DateTime.Now.ToString("HH:mm:ss.fff")}");
            return results;
        }
    }
}