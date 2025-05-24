using System.Diagnostics;
namespace VisualKeyloggerDetector.Core.Monitoring
{
    /// <summary>
    /// Stores the monitoring results for all targeted processes.
    /// The key is the Process ID (PID).
    /// The value is a list containing the number of bytes written during each consecutive monitoring interval.
    /// The list length should match the configured PatternLengthN.
    /// </summary>
    public class MonitoringResult : Dictionary<uint, ulong> { }

    /// <summary>
    /// Responsible for monitoring the I/O activity (specifically disk writes via WriteTransferCount)
    /// of specified running processes over a series of time intervals.
    /// Uses unprivileged APIs (WMI).
    /// </summary>
    public class Monitors
    {
        private readonly ExperimentConfiguration _config;

        /// <summary>
        /// Occurs when there is a status update message from the monitor.
        /// </summary>
        public event EventHandler<string> StatusUpdate;

        /// <summary>
        /// Occurs when the monitor completes an interval, reporting the index of the completed interval (0-based).
        /// </summary>
        public event EventHandler<int> ProgressUpdate;

        /// <summary>
        /// Raises the StatusUpdate event.
        /// </summary>
        /// <param name="message">The status message.</param>
        protected virtual void OnStatusUpdate(string message) => StatusUpdate?.Invoke(this, message);

        /// <summary>
        /// Raises the ProgressUpdate event.
        /// </summary>
        /// <param name="intervalIndex">The index of the interval just completed (0-based).</param>
        protected virtual void OnProgressUpdate(int intervalIndex) => ProgressUpdate?.Invoke(this, intervalIndex);
        public MonitoringResult results;
        public MonitoringResult lastWriteCounts;
        /// <summary>
        /// Initializes a new instance of the <see cref="Monitor"/> class.
        /// </summary>
        /// <param name="config">The experiment configuration containing parameters like N and T.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> is null.</exception>
        public Monitors(ExperimentConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            // OnStatusUpdate($"Starting monitoring for {processIdList.Count} process(es)...");
            results = new MonitoringResult();
            // Stores the last known WriteTransferCount for each process
            lastWriteCounts = new MonitoringResult();


        }

        /// <summary>
        /// Asynchronously monitors the WriteTransferCount of specified processes over N intervals,
        /// each of duration T milliseconds.
        /// </summary>
        /// <param name="processIdsToMonitor">An enumerable collection of Process IDs (PIDs) to monitor.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A Task resulting in a <see cref="MonitoringResult"/> dictionary containing the bytes written per interval for each monitored process.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled via the <paramref name="cancellationToken"/>.</exception>
        public async Task<MonitoringResult> MonitorProcessesAsync(IEnumerable<uint> processIdsToMonitor, CancellationToken cancellationToken = default)
        {
            var processIdList = processIdsToMonitor?.ToList() ?? new List<uint>();
            if (!processIdList.Any())
            {
                OnStatusUpdate("No processes specified for monitoring.");
                //return new MonitoringResult();
            }

            /*  OnStatusUpdate($"Starting monitoring for {processIdList.Count} process(es)...");
              var results = new MonitoringResult();
              // Stores the last known WriteTransferCount for each process
              var lastWriteCounts = new Dictionary<uint, ulong>();
            */  // Use HashSet for efficient checking if a PID is being monitored
            var processSet = new HashSet<uint>(processIdList);
            /*
              // Initialize results structure for expected processes
              foreach (uint pid in processSet)
              {
                  results[pid] = new List<ulong>(_config.PatternLengthN);
              }*/

            var stopwatch = new Stopwatch();
            /*  int intervalDuration = _config.IntervalDurationT;
              if (intervalDuration <= 0)
              {
                  OnStatusUpdate("Warning: Interval duration is zero or negative. Monitoring may not function correctly.");
                  intervalDuration = 1; // Use a minimal positive duration to avoid issues
              }
            */

            /*   // --- Initial Read (Baseline) ---
               OnStatusUpdate("Establishing baseline process write counts...");
               try
               {
                   // Uses the static helper class ProcessMonitor (defined elsewhere)
                   var initialProcessInfo = await ProcessMonitor.GetAllProcessesInfoAsync();
                   foreach (var pInfo in initialProcessInfo)
                   {
                       if (processSet.Contains(pInfo.Id))
                       {
                           lastWriteCounts[pInfo.Id] = pInfo.WriteCount;
                           // Ensure entry exists in results even if process disappears later
                           if (!results.ContainsKey(pInfo.Id))
                               results[pInfo.Id] = new List<ulong>(_config.PatternLengthN);
                       }
                   }
                   OnStatusUpdate($"Baseline established for {lastWriteCounts.Count} of {processSet.Count} target processes.");
               }
               catch (Exception ex)
               {
                   OnStatusUpdate($"Error getting initial process info: {ex.Message}. Proceeding without baseline for some processes.");
                   // Continue, but processes found later will have an assumed baseline of 0 for the first interval diff.
               }
            */

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
                        // lastWriteCounts[pid] = currentCount;
                    }
                    else
                    {
                        // Process disappeared or wasn't found in the current query.
                        lastWriteCounts.Remove(pid); // Stop tracking baseline for this PID
                    }

                    // Add result for this interval (even if 0 or process disappeared)
                    // Ensure the list exists before adding
                    /*  if (!results.ContainsKey(pid))
                          results[pid] = new List<ulong>(_config.PatternLengthN); // Should not happen if initialized correctly, but safety check

                      // Only add if the list isn't already full (e.g., due to errors)
                      if (results[pid].Count < _config.PatternLengthN)
                      {
                          results[pid].Add(bytesWrittenThisInterval);
                          Console.WriteLine($"PID {pid}: Interval {i + 1} - Bytes Written: {bytesWrittenThisInterval} {DateTime.Now.ToString("HH:mm:ss.fff")}");
                      }*/
                } // End foreach pid

                // OnStatusUpdate($"Interval {i + 1}/{_config.PatternLengthN}: Data collected.");
                stopwatch.Stop(); // Optional: Log if interval took longer than expected due to WMI query time

                //OnProgressUpdate(i); // Report progress after completing interval i

            } // End interval loop (i)

            // Ensure all result lists have the correct length, padding with 0 if necessary (e.g., if process disappeared)
            /* foreach (var pid in processSet)
             {
                 if (results.TryGetValue(pid, out var list))
                 {
                     while (list.Count < _config.PatternLengthN)
                     {
                         list.Add(0); // Pad missing intervals with 0
                     }
                 }
             }
            */

            OnStatusUpdate("Monitoring finished.");
            Debug.WriteLine($"Monitoring finished. {DateTime.Now.ToString("HH:mm:ss.fff")}");
            return results;
        }
    }
}