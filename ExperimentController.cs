using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VisualKeyloggerDetector;
using VisualKeyloggerDetector.Core.Detection;
using VisualKeyloggerDetector.Core.Injection;
using VisualKeyloggerDetector.Core.Monitoring;
using VisualKeyloggerDetector.Core.PatternGeneration;
using VisualKeyloggerDetector.Core.Translation;
//using VisualKeyloggerDetector.Core.Api;
//using keylogger_lib.Entities; // Reference keylogger lib

namespace VisualKeyloggerDetector.Core
{
    /// <summary>
    /// Orchestrates the keylogger detection experiment by coordinating the
    /// PatternGenerator, PatternTranslator, Injector, Monitor, and Detector components.
    /// Manages the overall workflow and reports status, progress, and results.
    /// </summary>
    public class ExperimentController : IDisposable
    {
        private readonly ExperimentConfiguration _config;
        private readonly PatternGenerator _patternGenerator;
        private readonly PatternTranslator _patternTranslator;
        private readonly Injector _injector;
        private readonly Monitors _monitor;
        private readonly Detector _detector;
        private CancellationTokenSource _cts;
        private volatile bool _isRunning = false; // Use volatile for thread safety on read/write

        // --- Events for UI updates ---

        /// <summary>
        /// Occurs when there is a status update message during the experiment.
        /// </summary>
        public event EventHandler<string> StatusUpdated;

        /// <summary>
        /// Occurs when a major step in the experiment progresses.
        /// Provides the current step index and the total number of steps.
        /// </summary>
        public event EventHandler<(int current, int total)> ProgressUpdated;

        /// <summary>
        /// Occurs when the experiment completes, providing the list of detection results.
        /// This event is raised on successful completion, cancellation, or error.
        /// </summary>
        public event EventHandler<List<DetectionResult>> ExperimentCompleted;


        public event EventHandler<DetectionResult> KeyloggerDetected;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentController"/> class.
        /// </summary>
        /// <param name="config">The configuration settings for the experiment.</param>
        /// <param name="patternAlgorithm">The algorithm used to generate the input pattern.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> or <paramref name="patternAlgorithm"/> is null.</exception>
        public ExperimentController(ExperimentConfiguration config, IPatternGeneratorAlgorithm patternAlgorithm)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (patternAlgorithm == null) throw new ArgumentNullException(nameof(patternAlgorithm));

            // Instantiate components, passing configuration where needed
            _patternGenerator = new PatternGenerator(patternAlgorithm);
            _patternTranslator = new PatternTranslator(_config);
            _injector = new Injector();
            _monitor = new Monitors(_config);
            _detector = new Detector();

            // Optional: Wire up internal component events to the controller's events if needed
            // Example: _injector.StatusUpdate += (s, msg) => OnStatusUpdated($"Injector: {msg}");
        }

        /// <summary>
        /// Raises the <see cref="StatusUpdated"/> event.
        /// </summary>
        /// <param name="message">The status message.</param>
        protected virtual void OnStatusUpdated(string message) => StatusUpdated?.Invoke(this, message);

        /// <summary>
        /// Raises the <see cref="ProgressUpdated"/> event.
        /// </summary>
        /// <param name="current">The current step index (0-based).</param>
        /// <param name="total">The total number of steps.</param>
        protected virtual void OnProgressUpdated(int current, int total) => ProgressUpdated?.Invoke(this, (current, total));

        /// <summary>
        /// Raises the <see cref="ExperimentCompleted"/> event.
        /// </summary>
        /// <param name="results">The list of detection results obtained.</param>
        protected virtual void OnExperimentCompleted(List<DetectionResult> results) => ExperimentCompleted?.Invoke(this, results);

        /// <summary>
        /// Gets a value indicating whether the experiment is currently running.
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Asynchronously starts the keylogger detection experiment.
        /// This involves generating patterns, identifying processes, running injection and monitoring concurrently,
        /// analyzing results, and writing output.
        /// </summary>
        /// <returns>A Task representing the asynchronous operation.</returns>
        public async Task StartExperimentAsync()
        {
            if (_isRunning)
            {
                OnStatusUpdated("Experiment is already running.");
                return;
            }

            _isRunning = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var overallResults = new List<DetectionResult>();
            // Define total major steps for progress reporting
            const int totalSteps = 6;

            try
            {
                // --- Step 1: Generate Input Pattern ---
                OnProgressUpdated(0, totalSteps);
                OnStatusUpdated("Step 1/6: Generating input pattern...");
                _config.file1.WriteLine("entering generator");
                AbstractKeystrokePattern inputPattern = _patternGenerator.GeneratePattern(_config.PatternLengthN,_config.file1);
                OnStatusUpdated($"Generated pattern using {_patternGenerator.AlgorithmTypeName} ({inputPattern.Length} samples).");
                token.ThrowIfCancellationRequested(); // Allow cancellation between steps

                // --- Step 2: Translate Pattern to Schedule ---
                OnProgressUpdated(1, totalSteps);
                OnStatusUpdated("Step 2/6: Translating pattern to injection schedule...");
                KeystrokeStreamSchedule schedule = _patternTranslator.TranslatePatternToStreamSchedule(inputPattern);
                OnStatusUpdated($"Created schedule for {schedule.TotalDurationMs}ms total duration.");
                token.ThrowIfCancellationRequested();

                // --- Step 3: Identify Candidate Processes ---
                OnProgressUpdated(2, totalSteps);
                OnStatusUpdated("Step 3/6: Identifying candidate processes...");
                List<ProcessInfoData> allProcesses;
                try
                {
                    allProcesses = await ProcessMonitor.GetAllProcessesInfoAsync();
                }
                catch (Exception ex)
                {
                    OnStatusUpdated($"ERROR during process query: {ex.Message}. Aborting experiment.");
                    throw; // Rethrow to be caught by the main catch block
                }
               // var candidateProcesses = FilterCandidateProcesses(allProcesses);
               _config.processInfoDatas = allProcesses.Where(p => p != null).ToList(); // Filter out nulls
                _config.ProcessIdsToMonitor = _config.processInfoDatas.Select(p => p.Id).ToList();
                int i = 1;
                foreach (var c in _config.ProcessIdsToMonitor)
                {
                    _config.file1.WriteLine($"Candidate id {i}  {c}");
                    i++;
                }
                if (!_config.ProcessIdsToMonitor.Any())
                {
                    OnStatusUpdated("No candidate processes found after filtering. Stopping experiment.");
                    OnExperimentCompleted(overallResults); // Complete with empty results
                    _isRunning = false;
                    return;
                }
                OnStatusUpdated($"Found {_config.ProcessIdsToMonitor.Count} candidate process(es) to monitor.");
                token.ThrowIfCancellationRequested();


                // --- Step 4: Run Monitor and Injector Concurrently ---
                OnProgressUpdated(3, totalSteps);
                OnStatusUpdated("Step 4/6: Starting concurrent monitoring and injection...");

                // Setup tasks
                // Task<MonitoringResult> monitoringTask = _monitor.MonitorProcessesAsync(candidatePids, token);
                Task<InjectorResult> injectionTask = _injector.InjectStreamAsync(schedule,_config, token);

                // Await both tasks to complete. If one throws (e.g., due to cancellation), WhenAll will rethrow.
                //  await Task.WhenAll(injectionTask);

                OnStatusUpdated("Monitoring and injection completed.");
                _config.file1.WriteLine("monitoring and injection completed");
                InjectorResult monitoringResult = await injectionTask; // Get the result (already awaited by WhenAll)
                token.ThrowIfCancellationRequested(); // Check cancellation again after tasks
                _config.file1.WriteLine($"Length of dictionary {monitoringResult.Count}");
                foreach (var pair in monitoringResult)
                {
                    foreach (var p in pair.Value)
                    {
                        _config.file1.WriteLine($"Key: {pair.Key}, Value: {p}");
                    }
                }

                // --- Step 5: Analyze Results ---
                OnProgressUpdated(4, totalSteps);
                OnStatusUpdated("Step 5/6: Analyzing collected data...");
                _config.file1.WriteLine("entering analysis");
                overallResults = AnalyzeMonitoringResults(inputPattern, monitoringResult, _config.processInfoDatas);
                OnStatusUpdated($"Analysis complete. Found {overallResults.Count(r => r.IsDetected)} potential detection(s).");
                token.ThrowIfCancellationRequested();

                // --- Step 6: Write Results ---
                OnProgressUpdated(5, totalSteps);
                OnStatusUpdated("Step 6/6: Writing results to file...");
                WriteResultsToFile(overallResults);
                OnStatusUpdated($"Results saved to {_config.ResultsFilePath}");

                OnProgressUpdated(totalSteps, totalSteps); // Final progress update
                OnExperimentCompleted(overallResults); // Signal successful completion
            }
            catch (OperationCanceledException)
            {
                OnStatusUpdated("Experiment cancelled by user.");
                OnExperimentCompleted(overallResults); // Report any partial results if needed (currently empty on cancel)
            }
            catch (Exception ex)
            {
                OnStatusUpdated($"ERROR: An unexpected error occurred: {ex.Message}");
                // Consider logging the full exception details (ex.ToString()) for debugging
                Console.WriteLine($"Experiment Error: {ex}");
                OnExperimentCompleted(overallResults); // Complete with potentially partial/empty results
            }
            finally
            {
                // Ensure running state is reset and CancellationTokenSource is disposed
                _isRunning = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        /// <summary>
        /// Requests cancellation of the currently running experiment.
        /// The cancellation is cooperative and may take some time to complete.
        /// </summary>
        public void StopExperiment()
        {
            if (_isRunning && _cts != null && !_cts.IsCancellationRequested)
            {
                OnStatusUpdated("Stopping experiment...");
                try
                {
                    _cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // Ignore if already disposed, race condition possible
                }
            }
            else if (!_isRunning)
            {
                OnStatusUpdated("Experiment is not running.");
            }
        }

        /// <summary>
        /// Filters the list of all running processes to identify candidates for monitoring.
        /// Excludes known safe processes, processes in system directories, and the idle process.
        /// </summary>
        /// <param name="allProcesses">A list of <see cref="ProcessInfoData"/> for all running processes.</param>
        /// <returns>A filtered list of candidate processes.</returns>
        private List<ProcessInfoData> FilterCandidateProcesses(List<ProcessInfoData> allProcesses)
        {
            if (allProcesses == null) return new List<ProcessInfoData>();

            var sw = Stopwatch.StartNew(); // Measure filtering time if needed
            var candidates = allProcesses.Where(p =>
                p != null &&
                p.Id != 0 && // Exclude Idle process (PID 0)
                !string.IsNullOrEmpty(p.Name) &&
                !_config.SafeProcessNames.Contains(p.Name) &&
                (string.IsNullOrEmpty(p.ExecutablePath) || // Keep if path is null (might be interesting, e.g., system processes not explicitly excluded)
                 !_config.ExcludedPathPrefixes.Any(prefix =>
                    p.ExecutablePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            ).ToList();
            sw.Stop();
            // OnStatusUpdated($"Filtering took {sw.ElapsedMilliseconds}ms"); // Optional performance log
            return candidates;
        }

        /// <summary>
        /// Analyzes the monitoring results by comparing the output pattern of each process
        /// with the original input pattern using the Detector.
        /// </summary>
        /// <param name="inputPattern">The original input <see cref="AbstractKeystrokePattern"/>.</param>
        /// <param name="monitoringResult">The <see cref="MonitoringResult"/> containing byte counts per interval for monitored processes.</param>
        /// <param name="candidateProcessInfo">Information about the processes that were monitored.</param>
        /// <returns>A list of <see cref="DetectionResult"/> for each analyzed process.</returns>
        private List<DetectionResult> AnalyzeMonitoringResults(
            AbstractKeystrokePattern inputPattern,
            InjectorResult monitoringResult,
            List<ProcessInfoData> candidateProcessInfo)
        {
            var detectionResults = new List<DetectionResult>();
            // Create a lookup map for quick access to process info by PID
            var processInfoMap = candidateProcessInfo.Where(p => p != null).ToDictionary(p => p.Id);
            Console.WriteLine($" monitoring result length in analyzer {monitoringResult.Count}");

            if (monitoringResult == null) return detectionResults;

            foreach (var kvp in monitoringResult)
            {
                uint pid = kvp.Key;
                Console.WriteLine($"analyze monitoring result {pid}");
                List<ulong> bytesPerInterval = kvp.Value;

                // Ensure we have info for this process and the data length is correct
                if (!processInfoMap.TryGetValue(pid, out var pInfo)) continue;
                if (bytesPerInterval == null || bytesPerInterval.Count != _config.PatternLengthN)
                {
                    OnStatusUpdated($"Warning: Data length mismatch for PID {pid}. Expected {_config.PatternLengthN}, got {bytesPerInterval?.Count ?? 0}. Skipping analysis.");
                    continue;
                }

                // --- Filtering based on activity ---
                // Calculate average bytes written during the monitored intervals.
                // Use Average() which handles empty list returning NaN, check for that.
                double avgBytes = bytesPerInterval.Any() ? bytesPerInterval.Average(b => (double)b) : 0.0;

                // Skip processes with very low average writes during tests, below the configured threshold.
                if (avgBytes < _config.MinAverageWriteBytesPerInterval)
                {
                    Console.WriteLine($"exited loop due to less average writecount {pid}");
                    continue;
                }
                // --- Analysis ---
                // Translate the byte stream into a normalized output pattern (AKP).
                AbstractKeystrokePattern outputPattern = _patternTranslator.TranslateByteCountsToPattern(pid, bytesPerInterval);

                // Calculate the Pearson Correlation Coefficient (PCC) between input and output patterns.
                double pcc = _detector.CalculatePCC(inputPattern, outputPattern);

                // Create the result object, storing relevant information.
                detectionResults.Add(new DetectionResult
                {
                    ProcessId = pid,
                    ProcessName = pInfo.Name,
                    ExecutablePath = pInfo.ExecutablePath,
                    Correlation = pcc, // Can be NaN
                    AverageBytesWrittenPerInterval = avgBytes,
                    Threshold = _config.DetectionThreshold // Store threshold used for this analysis
                });
            }
            //notification system integration
            foreach (var result in detectionResults)
            {
                if (result.IsDetected)
                {
                    OnStatusUpdated($"DETECTION: PID {result.ProcessId} ({result.ProcessName}) - PCC: {result.Correlation:F4}");
                    // Option 1: Show notification directly (simpler for now)
                    // Utils.NotificationHelper.ShowDetectionNotification(result);

                    // Option 2: Raise event for UI to handle (better design)
                    KeyloggerDetected?.Invoke(this, result);
                }
            }

            // Sort results for better presentation (e.g., by correlation descending, handling NaN)
            /* detectionResults.Sort((a, b) =>
             {
                 // Put NaN values at the end
               if (double.IsNaN(a.Correlation) && double.IsNaN(b.Correlation)) return 0;
                 if (double.IsNaN(a.Correlation)) return 1;
                 if (double.IsNaN(b.Correlation)) return -1;
                 // Sort by correlation descending
                 return b.Correlation.CompareTo(a.Correlation);
             });
            */

            return detectionResults;
        }

        /// <summary>
        /// Writes the final detection results to the configured text file.
        /// </summary>
        /// <param name="results">The list of <see cref="DetectionResult"/> to write.</param>
        private void WriteResultsToFile(List<DetectionResult> results)
        {
            try
            {
                // Use FileMode.Create to overwrite the file if it exists
                using (var file = new StreamWriter(_config.ResultsFilePath, append: false, System.Text.Encoding.UTF8))
                {
                    file.WriteLine($"Keylogger Detection Results - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    file.WriteLine($"==================================================");
                    file.WriteLine($"Pattern Algorithm: {_patternGenerator.AlgorithmTypeName}");
                    file.WriteLine($"Configuration:");
                    file.WriteLine($"  Intervals (N): {_config.PatternLengthN}");
                    file.WriteLine($"  Interval Duration (T): {_config.IntervalDurationT} ms");
                    file.WriteLine($"  Key Range (Kmin-Kmax): {_config.MinKeysPerIntervalKmin}-{_config.MaxKeysPerIntervalKmax}");
                    file.WriteLine($"  Detection Threshold (PCC): > {_config.DetectionThreshold:F2}");
                    file.WriteLine($"  Min Avg Write Filter: > {_config.MinAverageWriteBytesPerInterval:N1} bytes/interval");
                    file.WriteLine($"==================================================");
                    file.WriteLine();

                    var detected = results.Where(r => r.IsDetected).ToList();
                    var notDetected = results.Where(r => !r.IsDetected).ToList();


                    if (detected.Any())
                    {
                        file.WriteLine($"*** POSSIBLE KEYLOGGERS DETECTED ({detected.Count}) ***");
                        file.WriteLine($"--------------------------------------------------");
                        foreach (var result in detected)
                        {
                            file.WriteLine($"  Process: {result.ProcessName} (PID: {result.ProcessId})");
                            file.WriteLine($"  Path:    {result.ExecutablePath ?? "N/A"}");
                            file.WriteLine($"  PCC:     {result.Correlation:F4} (Threshold: > {_config.DetectionThreshold:F2})");
                            file.WriteLine($"  AvgWrite:{result.AverageBytesWrittenPerInterval:N1} bytes/interval");
                            file.WriteLine($"--------------------------------------------------");
                        }
                    }
                    else
                    {
                        file.WriteLine(">>> No processes met the detection criteria (PCC > Threshold).");
                        file.WriteLine();
                    }

                    if (notDetected.Any())
                    {
                        file.WriteLine();
                        file.WriteLine($"--- Other Monitored Processes ({notDetected.Count}) ---");
                        file.WriteLine($"(Processes passing write filter but below PCC threshold or with NaN correlation)");
                        file.WriteLine($"--------------------------------------------------");
                        foreach (var result in notDetected)
                        {
                            file.WriteLine($"  Process: {result.ProcessName} (PID: {result.ProcessId})");
                            file.WriteLine($"  Path:    {result.ExecutablePath ?? "N/A"}");
                            file.WriteLine($"  PCC:     {(double.IsNaN(result.Correlation) ? "NaN" : result.Correlation.ToString("F4"))}");
                            file.WriteLine($"  AvgWrite:{result.AverageBytesWrittenPerInterval:N1} bytes/interval");
                            file.WriteLine($"--------------------------------------------------");
                        }
                    }
                    file.WriteLine();
                    file.WriteLine("End of Report");
                }
            }
            catch (Exception ex)
            {
                // Report error but don't crash the controller
                OnStatusUpdated($"Error writing results file '{_config.ResultsFilePath}': {ex.Message}");
            }
        }


        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// Stops the experiment if running and disposes the CancellationTokenSource.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            // No unmanaged resources to dispose directly here, but good practice pattern
            if (disposing)
            {
                // Dispose managed resources
                StopExperiment(); // Ensure cancellation is triggered
                _cts?.Dispose();
                _cts = null;
            }
        }

        // Finalizer (optional, only if you have unmanaged resources directly in this class)
        // ~ExperimentController()
        // {
        //     Dispose(false);
        // }

        // In ExperimentController.cs



    }
}