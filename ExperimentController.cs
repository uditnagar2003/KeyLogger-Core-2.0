using System.Diagnostics;
using VisualKeyloggerDetector.Core.Detection;
using VisualKeyloggerDetector.Core.Injection;
using VisualKeyloggerDetector.Core.Monitoring;
using VisualKeyloggerDetector.Core.PatternGeneration;
using VisualKeyloggerDetector.Core.Translation;
//using VisualKeyloggerDetector.Core.Api;
//using keylogger_lib.Entities; // Reference keylogger lib

namespace VisualKeyloggerDetector.Core
{
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

       
        public event EventHandler<string> StatusUpdated;

    
        public event EventHandler<(int current, int total)> ProgressUpdated;

       
        public event EventHandler<List<DetectionResult>> ExperimentCompleted;


        public event EventHandler<DetectionResult> KeyloggerDetected;

        public event EventHandler<ProcessWriteInfoData> ProcessWriteCount;


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

            
        }

        //injector events
       

        protected virtual void OnStatusUpdated(string message) => StatusUpdated?.Invoke(this, message);

        protected virtual void OnProgressUpdated(int current, int total) => ProgressUpdated?.Invoke(this, (current, total));

        protected virtual void OnExperimentCompleted(List<DetectionResult> results) => ExperimentCompleted?.Invoke(this, results);

        public bool IsRunning => _isRunning;

        public async Task StartExperimentAsync()
        {
            // Subscribe to injector status updates
            _injector.StatusUpdate += (s, msg) => OnStatusUpdated($"Injector: {msg}");
            _injector.ProcessInfoUpdate+= (s, data) => ProcessWriteCount?.Invoke(this, data);
           
            _injector.ProgressUpdate += (s,pro) => OnProgressUpdated(pro,6+_config.PatternLengthN);
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
             int totalSteps = 5+_config.PatternLengthN;
            OnStatusUpdated("Starting experiment...");

            try
            {
                // --- Step 1: Generate Input Pattern ---
                OnProgressUpdated(0, totalSteps);
                OnStatusUpdated("Step 1/6: Generating input pattern...");
                Debug.WriteLine("entering generator");
                AbstractKeystrokePattern inputPattern = _patternGenerator.GeneratePattern(_config.PatternLengthN);
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

                allProcesses = FilterCandidateProcesses(allProcesses);
                foreach(var p in allProcesses)
                {
                    _config.ProcessInfoDataMap[p.Id] = p; // Populate the map for quick access
                   
                }
                // _config.processInfoDatas = allProcesses.Where(p => p != null).ToList(); // Filter out nulls
                _config.ProcessIdsToMonitor = _config.ProcessInfoDataMap.Keys.ToList();//processInfoDatas.Select(p => p.Id).ToList();
                int length = _config.ProcessIdsToMonitor.Count;
                Debug.WriteLine($"INfo process legthn {length}");
                for (int i = 0; i < length; i++)
                {
                    Debug.WriteLine($"Candidate id {i + 1}  {_config.ProcessIdsToMonitor[i]}");
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
                Task<InjectorResult> injectionTask = _injector.InjectStreamAsync(schedule, _config, token);

                // Await both tasks to complete. If one throws (e.g., due to cancellation), WhenAll will rethrow.
                //  await Task.WhenAll(injectionTask);

                OnStatusUpdated("Monitoring and injection completed.");
                Debug.WriteLine("monitoring and injection completed");
                InjectorResult monitoringResult = await injectionTask; // Get the result (already awaited by WhenAll)
                token.ThrowIfCancellationRequested(); // Check cancellation again after tasks
                Debug.WriteLine($"Length of dictionary {monitoringResult.Count}");
                foreach (var pair in monitoringResult)
                {
                    foreach (var p in pair.Value)
                    {
                        Debug.WriteLine($"Key: {pair.Key}, Value: {p}");
                    }
                }

                // --- Step 5: Analyze Results ---
                OnProgressUpdated(4+_config.PatternLengthN, totalSteps);
                OnStatusUpdated("Step 5/6: Analyzing collected data...");
                Debug.WriteLine("entering analysis");
                overallResults = AnalyzeMonitoringResults(inputPattern, monitoringResult, _config.ProcessInfoDataMap);
                OnStatusUpdated($"Analysis complete. Found {overallResults.Count(r => r.IsDetected)} potential detection(s).");
                Debug.WriteLine($"Analysis complete. Found {overallResults.Count(r => r.IsDetected)} potential detection(s).");
                token.ThrowIfCancellationRequested();

                // --- Step 6: Write Results ---
                OnProgressUpdated(5+_config.PatternLengthN, totalSteps);
                OnStatusUpdated("Step 6/6: Writing results to file...");
                WriteResultsToFile(overallResults);
                OnStatusUpdated($"Results saved to {_config.ResultsFilePath}");

                OnProgressUpdated(totalSteps, totalSteps); // Final progress update
                OnExperimentCompleted(overallResults); // Signal successful completion*/
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

        private List<DetectionResult> AnalyzeMonitoringResults(
            AbstractKeystrokePattern inputPattern,
            InjectorResult monitoringResult,
            Dictionary<uint, ProcessInfoData> processInfoMap)
        {
            var detectionResults = new List<DetectionResult>();
            // Create a lookup map for quick access to process info by PID
          //  var processInfoMap = candidateProcessInfo.Where(p => p != null).ToDictionary(p => p.Id);
            Debug.WriteLine($" monitoring result length in analyzer {monitoringResult.Count}");

            if (monitoringResult == null) return detectionResults;

            foreach (var kvp in monitoringResult)
            {
                uint pid = kvp.Key;
                Debug.WriteLine($"analyze monitoring result {pid}");
                List<ulong> bytesPerInterval = kvp.Value;

                // Ensure we have info for this process and the data length is correct
                if (!processInfoMap.TryGetValue(pid, out var pInfo))
                {
                    Debug.WriteLine($"exited loop due to abencse of process id {pid}");

                    continue;
                }
                if (bytesPerInterval == null || bytesPerInterval.Count != _config.PatternLengthN)
                {
                    OnStatusUpdated($"Warning: Data length mismatch for PID {pid}. Expected {_config.PatternLengthN}, got {bytesPerInterval?.Count ?? 0}. Skipping analysis.");
                    Debug.WriteLine($"exited loop due to data length mismatch {pid}");
                    continue;
                }

                // --- Filtering based on activity ---
                // Calculate average bytes written during the monitored intervals.
                // Use Average() which handles empty list returning NaN, check for that.
                double avgBytes = bytesPerInterval.Any() ? bytesPerInterval.Average(b => (double)b) : 0.0;

                // Skip processes with very low average writes during tests, below the configured threshold.
                if (avgBytes < _config.MinAverageWriteBytesPerInterval)
                {
                    Debug.WriteLine($"exited loop due to less average writecount {pid}");
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
                    DetectionTime = DateTime.Now,
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

            return detectionResults;
        }

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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

       
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