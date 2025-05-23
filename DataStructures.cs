using System;
using System.Collections.Generic;
using System.IO; // Added for Path
using System.Linq; // Added for Linq

namespace VisualKeyloggerDetector.Core
{
    /// <summary>
    /// Represents a normalized pattern of activity (keystrokes or bytes)
    /// over discrete time intervals. Samples are intended to be in the range [0, 1].
    /// This corresponds to the Abstract Keystroke Pattern (AKP) in the paper.
    /// </summary>
    public class AbstractKeystrokePattern
    {
        /// <summary>
        /// Gets the list of normalized samples representing the pattern.
        /// Each sample corresponds to one time interval.
        /// </summary>
        public List<double> Samples { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbstractKeystrokePattern"/> class.
        /// </summary>
        /// <param name="samples">The list of normalized samples (expected range [0, 1]).</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="samples"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if any sample is outside the range [0, 1].</exception>
        public AbstractKeystrokePattern(List<double> samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            // Basic validation: ensure samples are within [0, 1] using a small tolerance for floating point
            /* const double epsilon = 1e-9;
             if (samples.Any(s => s < (0.0 - epsilon) || s > (1.0 + epsilon)))
             {
                 // Find the offending sample for a better error message
                 double offendingSample = samples.First(s => s < (0.0 - epsilon) || s > (1.0 + epsilon));
                 throw new ArgumentException($"Samples must be between 0.0 and 1.0. Found: {offendingSample}", nameof(samples));
             }*/
            Samples = new List<double>(samples); // Create a defensive copy
        }

        /// <summary>
        /// Gets the number of samples (time intervals) in the pattern.
        /// </summary>
        public int Length => Samples.Count;
    }

    /// <summary>
    /// Holds configuration parameters for the experiment execution, translation, and detection phases.
    /// </summary>
    public class ExperimentConfiguration
    {
        /// <summary>
        /// Gets or sets the number of samples (time intervals) in the pattern (N).
        /// </summary>
        public int PatternLengthN { get; set; } = 10;

        /// <summary>
        /// Gets or sets the duration of each time interval in milliseconds (T).
        /// </summary>
        public int IntervalDurationT { get; set; } = 100;

        public int T { get; set; } = 1000;
        /// <summary>
        /// Gets or sets the minimum number of keystrokes expected/generated within one interval (T).
        /// Used for normalization/denormalization (Kmin).
        /// </summary>
        public int MinKeysPerIntervalKmin { get; set; } = 5;

        /// <summary>
        /// Gets or sets the maximum number of keystrokes expected/generated within one interval (T).
        /// Used for normalization/denormalization (Kmax). Must be greater than Kmin.
        /// </summary>
        public int MaxKeysPerIntervalKmax { get; set; } = 10;

        /// <summary>
        /// Gets or sets the correlation threshold (PCC value) for detection.
        /// A positive correlation greater than this value triggers detection.
        /// </summary>
        public double DetectionThreshold { get; set; } = 0.7;

        /// <summary>
        /// Gets or sets the minimum average bytes written per interval for a process to be considered a candidate after monitoring.
        /// Helps filter out processes with negligible I/O activity during the test.
        /// </summary>
        public double MinAverageWriteBytesPerInterval { get; set; } = 200;

        /// <summary>
        /// Gets or sets the full path to write the detection results file.
        /// </summary>
        public string ResultsFilePath { get; set; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "detector_results_v2.txt");

        /// <summary>
        /// Gets or sets a set of process names (case-insensitive) to exclude from monitoring and analysis (e.g., known safe system processes).
        /// </summary>
        public HashSet<string> SafeProcessNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Add other known safe processes
        };

        /// <summary>
        /// Gets or sets a list of path prefixes (case-insensitive). Processes whose executable path starts with
        /// any of these prefixes will be excluded from monitoring and analysis.
        /// </summary>
        public List<string> ExcludedPathPrefixes { get; set; } = new List<string>
        {
            /*  @"C:\Windows\",
              @"C:\Program Files\",
              @"C:\Program Files (x86)\"
              // Add other known safe system/application directories
          */
        };
    }

    /// <summary>
    /// Holds the result of the detection analysis for a single monitored process.
    /// </summary>
    public class DetectionResult
    {
        /// <summary>
        /// Gets or sets the name of the process.
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Gets or sets the process ID (PID).
        /// </summary>
        public uint ProcessId { get; set; }

        /// <summary>
        /// Gets or sets the executable path of the process, if available.
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Gets or sets the calculated Pearson Correlation Coefficient (PCC) between the input pattern
        /// and the process's output pattern. Value ranges from -1.0 to 1.0, or NaN if calculation failed.
        /// </summary>
        public double Correlation { get; set; }

        /// <summary>
        /// Gets or sets the average number of bytes written by the process per monitoring interval.
        /// </summary>
        public double AverageBytesWrittenPerInterval { get; set; }

        /// <summary>
        /// Gets or sets the detection threshold used when evaluating this result.
        /// </summary>
        public double Threshold { get; set; }

        /// <summary>
        /// Gets a value indicating whether this process is considered detected based on the correlation and threshold.
        /// Detection is triggered if the positive correlation exceeds the threshold.
        /// </summary>
        public bool IsDetected => !double.IsNaN(Correlation) && Correlation > Threshold;
    }
}