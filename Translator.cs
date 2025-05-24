using System;
using System.Collections.Generic;
using System.Linq;

namespace VisualKeyloggerDetector.Core.Translation
{
    /// <summary>
    /// Represents the schedule of keystrokes to be injected over discrete time intervals.
    /// This is the output of translating an input AKP for the Injector component.
    /// </summary>
    public class KeystrokeStreamSchedule
    {
        /// <summary>
        /// Gets the list containing the number of keystrokes to inject in each consecutive interval.
        /// </summary>
        public List<int> KeysPerInterval { get; }

        /// <summary>
        /// Gets the duration of each individual time interval in milliseconds.
        /// </summary>
        public int IntervalDurationMs { get; }

        /// <summary>
        /// Gets the total duration of the entire schedule in milliseconds.
        /// </summary>
        public int TotalDurationMs => KeysPerInterval.Count * IntervalDurationMs;

        /// <summary>
        /// Initializes a new instance of the <see cref="KeystrokeStreamSchedule"/> class.
        /// </summary>
        /// <param name="keysPerInterval">The list defining the number of keys for each interval.</param>
        /// <param name="intervalDurationMs">The duration of each interval in milliseconds.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="keysPerInterval"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="intervalDurationMs"/> is not positive.</exception>
        public KeystrokeStreamSchedule(List<int> keysPerInterval, int intervalDurationMs)
        {
            KeysPerInterval = keysPerInterval ?? throw new ArgumentNullException(nameof(keysPerInterval));
            if (intervalDurationMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalDurationMs), "Interval duration must be positive.");
            IntervalDurationMs = intervalDurationMs;
        }
    }

    /// <summary>
    /// Translates between the Pattern Domain (Abstract Keystroke Patterns) and the Stream Domain
    /// (keystroke schedules or byte counts per interval), based on configuration parameters.
    /// </summary>
    public class PatternTranslator
    {
        private readonly ExperimentConfiguration _config;

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternTranslator"/> class.
        /// </summary>
        /// <param name="config">The experiment configuration containing parameters like N, T, Kmin, Kmax.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="config"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if Kmax is not greater than Kmin in the configuration.</exception>
        public PatternTranslator(ExperimentConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (_config.MaxKeysPerIntervalKmax <= _config.MinKeysPerIntervalKmin)
            {
                throw new ArgumentException("Configuration error: MaxKeysPerIntervalKmax must be greater than MinKeysPerIntervalKmin.");
            }
        }

        /// <summary>
        /// Transforms a normalized input pattern (AKP) into a schedule specifying the number of keystrokes
        /// to inject during each time interval.
        /// </summary>
        /// <param name="inputPattern">The input <see cref="AbstractKeystrokePattern"/>.</param>
        /// <returns>A <see cref="KeystrokeStreamSchedule"/> for the Injector.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="inputPattern"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the input pattern length does not match the configured PatternLengthN.</exception>
        public KeystrokeStreamSchedule TranslatePatternToStreamSchedule(AbstractKeystrokePattern inputPattern)
        {
            if (inputPattern == null) throw new ArgumentNullException(nameof(inputPattern));
            if (inputPattern.Length != _config.PatternLengthN)
                throw new ArgumentException($"Input pattern length ({inputPattern.Length}) must match configuration N ({_config.PatternLengthN}).");

            var keysPerInterval = new List<int>(_config.PatternLengthN);
            double kRange = _config.MaxKeysPerIntervalKmax - _config.MinKeysPerIntervalKmin;

            foreach (double samplePi in inputPattern.Samples)
            {
                // Denormalize: Keys = Pi * (Kmax - Kmin) + Kmin
                // Calculate the target number of keys for this interval based on the normalized sample.
                double targetKeysExact = (samplePi * kRange + _config.MinKeysPerIntervalKmin);///_config.T;
                keysPerInterval.Add((int)Math.Round(targetKeysExact)); // Round to nearest integer
            }
            int i = 0;
            foreach (int ind in keysPerInterval)
                _config.file1.WriteLine($" {i+1} sample 1normalized to akp" + ind);
            return new KeystrokeStreamSchedule(keysPerInterval, _config.IntervalDurationT);
        }

        /// <summary>
        /// Transforms a stream of byte counts (one count per interval) into a normalized output pattern (AKP).
        /// Uses the configured Kmin and Kmax (defined for keystrokes) to normalize the byte counts,
        /// implicitly handling linear scaling transformations performed by the keylogger (e.g., fixed bytes per keystroke).
        /// </summary>
        /// <param name="bytesWrittenPerInterval">A list containing the number of bytes written by a process during each consecutive monitoring interval.</param>
        /// <returns>A normalized output <see cref="AbstractKeystrokePattern"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="bytesWrittenPerInterval"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the byte stream length does not match the configured PatternLengthN.</exception>
        public AbstractKeystrokePattern TranslateByteCountsToPattern(uint pid, List<ulong> bytesWrittenPerInterval)
        {
            if (bytesWrittenPerInterval == null) throw new ArgumentNullException(nameof(bytesWrittenPerInterval));
            if (bytesWrittenPerInterval.Count != _config.PatternLengthN)
                throw new ArgumentException($"Byte stream length ({bytesWrittenPerInterval.Count}) must match configuration N ({_config.PatternLengthN}).");

            var outputSamples = new List<double>(_config.PatternLengthN);
            double kRange = _config.MaxKeysPerIntervalKmax - _config.MinKeysPerIntervalKmin;

            foreach (ulong bytesWritten in bytesWrittenPerInterval)
            {
               // Console.WriteLine($"Bytes written for {pid}   " + bytesWritten);
                double normalizedSample;
                // Avoid division by zero if Kmax == Kmin (should be prevented by constructor check, but defensive)
                if (kRange <= 0)
                {
                    // If range is zero, normalize based on whether value meets the minimum
                    normalizedSample = (bytesWritten >= (ulong)_config.MinKeysPerIntervalKmin) ? 1.0 : 0.0;
                }
                else
                {
                    //: Pi = (Bytes_i - Kmin) / (Kmax - Kmin)
                    // Use Kmin/Kmax defined for keys, applying them to bytes.
                    normalizedSample = ((double)bytesWritten/*_config.T*/ - _config.MinKeysPerIntervalKmin) / kRange;
                }

                /* // Clamp the value to [0, 1] as byte counts might exceed Kmax or be below Kmin due to noise or scaling.
                normalizedSample = Math.Max(0.0, Math.Min(1.0, normalizedSample));
                 */

                outputSamples.Add(normalizedSample);
            }
            int i = 0;
            foreach (int ind in outputSamples)
                _config.file1.WriteLine($"{i+1} akp to normalized for {pid}  " + ind);
            return new AbstractKeystrokePattern(outputSamples);
        }
    }
}