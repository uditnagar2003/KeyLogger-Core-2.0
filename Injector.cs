using System.Diagnostics;
using VisualKeyloggerDetector.Core.Monitoring;
using VisualKeyloggerDetector.Core.Translation; // Needs access to KeystrokeStreamSchedule

namespace VisualKeyloggerDetector.Core.Injection
{
    public class InjectorResult : Dictionary<uint, List<ulong>> { }
    /// <summary>
    /// Responsible for injecting simulated keystrokes into the system based on a schedule.
    /// Uses unprivileged APIs to mimic user input.
    /// </summary>
    public class Injector
    {
        private readonly Random _random = new Random();
        // Characters to inject. Can be customized.
        private readonly string _charsToInject = "abcdefghijklmnopqrstuvwxyz";

        private ExperimentConfiguration _config;
        MonitoringResult monitoringResult;
        /// <summary>
        /// Occurs when there is a status update message from the injector.
        /// </summary>
        public event EventHandler<string> StatusUpdate;

        /// <summary>
        /// Occurs when the injector completes an interval, reporting the index of the completed interval (0-based).
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

        /// <summary>
        /// Asynchronously injects keystrokes according to the provided schedule.
        /// Attempts to distribute the keys somewhat evenly within each interval.
        /// </summary>
        /// <param name="schedule">The schedule defining the number of keys per interval and interval duration.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A Task representing the asynchronous injection operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="schedule"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled via the <paramref name="cancellationToken"/>.</exception>
        public async Task<InjectorResult> InjectStreamAsync(KeystrokeStreamSchedule schedule, ExperimentConfiguration _config1, CancellationToken cancellationToken = default)
        {
            _config = _config1 ?? throw new ArgumentNullException(nameof(_config1));
            if (schedule == null) throw new ArgumentNullException(nameof(schedule));

            OnStatusUpdate("Starting keystroke injection...");
            Debug.WriteLine("Starting keystroke injection... " + DateTime.Now.ToString("HH:mm:ss.fff"));
            int totalIntervals = schedule.KeysPerInterval.Count;
            var stopwatch = new Stopwatch();

            var results = new InjectorResult();
            var processIdList = _config.ProcessIdsToMonitor?.ToList() ?? new List<uint>();
            var processSet = new HashSet<uint>(processIdList);
            // Initialize results structure for expected processes
            foreach (uint pid in processSet)
            {
                results[pid] = new List<ulong>(_config.PatternLengthN);
            }

            var objectsToMonitor = new Monitors(_config);
            /*
                        // --- Initial Read (Baseline) ---
                        OnStatusUpdate("Establishing baseline process write counts...");
                        try
                        {
                            // Uses the static helper class ProcessMonitor (defined elsewhere)
                            var initialProcessInfo = await ProcessMonitor.GetAllProcessesInfoAsync();
                            foreach (var pInfo in initialProcessInfo)
                            {
                                if (processSet.Contains(pInfo.Id))
                                {
                                    // objectsToMonitor.lastWriteCounts[pInfo.Id] = pInfo.WriteCount;
                                    // Ensure entry exists in results even if process disappears later
                                    if (!results.ContainsKey(pInfo.Id))
                                        results[pInfo.Id] = new List<ulong>(_config.PatternLengthN);
                                }
                            }
                            OnStatusUpdate($"Baseline established for {objectsToMonitor.lastWriteCounts.Count} of {processSet.Count} target processes.");
                        }
                        catch (Exception ex)
                        {
                            OnStatusUpdate($"Error getting initial process info: {ex.Message}. Proceeding without baseline for some processes.");
                            // Continue, but processes found later will have an assumed baseline of 0 for the first interval diff.
                        }
            */

            for (int i = 0; i < totalIntervals; i++)
            {
                // Check for cancellation at the start of each interval
                cancellationToken.ThrowIfCancellationRequested();

                int keysInThisInterval = schedule.KeysPerInterval[i];
                int intervalDuration = schedule.IntervalDurationMs;
                OnStatusUpdate($"Interval {i + 1}/{totalIntervals}: Injecting {keysInThisInterval} keys over {intervalDuration}ms.");
                Debug.WriteLine($"Interval {i + 1}/{totalIntervals}: Injecting {keysInThisInterval} keys over {intervalDuration}ms. " + DateTime.Now.ToString("HH:mm:ss.fff"));
                stopwatch.Restart();

                if (keysInThisInterval > 0 && intervalDuration > 0) // Ensure duration is positive for delay calculation
                {
                    // Distribute keys somewhat evenly within the interval
                    // Calculate average delay, handling potential division by zero if intervalDuration is 0
                    double delayBetweenKeys = (double)intervalDuration / keysInThisInterval;
                    double accumulatedDelayError = 0; // Accumulates fractional parts of delays
                    var initialProcessInfo = await ProcessMonitor.GetAllProcessesInfoAsync();
                    foreach (var pInfo in initialProcessInfo)
                    {
                        if (processSet.Contains(pInfo.Id))
                        {
                            objectsToMonitor.lastWriteCounts[pInfo.Id] = pInfo.WriteCount;
                            if (!results.ContainsKey(pInfo.Id))
                                results[pInfo.Id] = new List<ulong>(_config.PatternLengthN);
                        }
                        else
                        {
                            _config.processInfoDatas.Add(new ProcessInfoData
                            {
                                Id = pInfo.Id,
                                Name = pInfo.Name,
                                ExecutablePath = pInfo.ExecutablePath,
                                WriteCount = pInfo.WriteCount
                            });
                            // Fix for CS8602: Dereference of a possibly null reference.
                            if (_config.ProcessIdsToMonitor != null)
                            {
                                _config.ProcessIdsToMonitor.Add(pInfo.Id);
                            }

                            processSet.Add(pInfo.Id);
                            processIdList.Add(pInfo.Id);
                            results[pInfo.Id] = new List<ulong>(_config.PatternLengthN);
                            objectsToMonitor.lastWriteCounts[pInfo.Id] = pInfo.WriteCount; // Initialize to 0 for non-target processes
                        }
                    }
                    for (int k = 0; k < keysInThisInterval; k++)
                    {
                        // Check for cancellation before each key injection
                        cancellationToken.ThrowIfCancellationRequested();

                        // Inject a random character
                        try
                        {
                            char charToSend = _charsToInject[_random.Next(_charsToInject.Length)];
                            // Uses the static helper class KeyInputInjector (defined elsewhere)
                            KeyInputInjector.SendCharacter(charToSend);
                            // Console.WriteLine($"Injected Charater {DateTime.Now.ToString("HH:mm:ss.fff")} " + charToSend);
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue injection if possible
                            OnStatusUpdate($"Error sending key: {ex.Message}. Skipping key.");
                        }

                        // Calculate delay until the *next* key injection
                        // Only delay if there are more keys to send in this interval
                        if (k < keysInThisInterval - 1)
                        {
                            double currentDelay = delayBetweenKeys + accumulatedDelayError;
                            int waitTimeMs = (int)Math.Floor(currentDelay);
                            accumulatedDelayError = currentDelay - waitTimeMs; // Carry over the fractional part

                            if (waitTimeMs > 0)
                            {
                                await Task.Delay(waitTimeMs, cancellationToken);
                            }
                        }
                    } // End key loop (k)
                } // End if keysInThisInterval > 0

                stopwatch.Stop();

                Task<MonitoringResult> result;
                Debug.WriteLine($"Monitoring processes for interval {i + 1} at {DateTime.Now.ToString("HH:mm:ss.fff")}");
                result = objectsToMonitor.MonitorProcessesAsync(processIdList, cancellationToken);

                monitoringResult = await result;
                foreach (uint pid in processSet)
                {
                    if (!results.ContainsKey(pid))
                        results[pid] = new List<ulong>(_config.PatternLengthN); // Should not happen if initialized correctly, but safety check

                    // Only add if the list isn't already full (e.g., due to errors)
                    try
                    {
                        if (results[pid].Count < _config.PatternLengthN && monitoringResult.ContainsKey(pid))
                        {
                            results[pid].Add(monitoringResult[pid]);
                            // Console.WriteLine($"PID {pid}: Interval {i + 1} - Bytes Written: {monitoringResult[pid]} {DateTime.Now.ToString("HH:mm:ss.fff")}");
                        }
                    }
                    catch
                    {
                        Debug.WriteLine("error not finding the process id in monitoring result");
                    }
                }

                // Ensure the full interval duration is respected by waiting for any remaining time.
                int elapsedTime = (int)stopwatch.ElapsedMilliseconds;
                int remainingTime = intervalDuration - elapsedTime + _config.T;
                if (remainingTime > 0)
                {
                    await Task.Delay(remainingTime, cancellationToken);
                }
                await Task.Delay(1000, cancellationToken); // Optional delay before finishing

                //Console.WriteLine($"interval ended {DateTime.Now.ToString("HH:mm:ss.fff")} " + i);
                OnProgressUpdate(i); // Report progress after completing interval i

            } // End interval loop (i)
            foreach (var pid in processSet)
            {
                if (results.TryGetValue(pid, out var list))
                {
                    while (list.Count < _config.PatternLengthN)
                    {
                        list.Add(0); // Pad missing intervals with 0
                    }
                }
            }
            InjectorResult filteredResult = new InjectorResult();

            foreach (var kvp in results)
            {
                int zeroCount = kvp.Value.Count(b => b == 0);
                int halfLength = kvp.Value.Count / 2;

                if (zeroCount < halfLength)
                {
                    filteredResult[kvp.Key] = kvp.Value;
                }
            }
            OnProgressUpdate(totalIntervals - 1); // Indicate completion of the last interval
            OnStatusUpdate("Injection finished.");
            return filteredResult;

        }
    }
}