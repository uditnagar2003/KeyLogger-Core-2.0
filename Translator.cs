using System.Diagnostics;

namespace VisualKeyloggerDetector.Core.Translation
{
    public class KeystrokeStreamSchedule
    {
        
        public List<int> KeysPerInterval { get; }

      
        public int IntervalDurationMs { get; }

       
        public int TotalDurationMs => KeysPerInterval.Count * IntervalDurationMs;

      
        public KeystrokeStreamSchedule(List<int> keysPerInterval, int intervalDurationMs)
        {
            KeysPerInterval = keysPerInterval ?? throw new ArgumentNullException(nameof(keysPerInterval));
            if (intervalDurationMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalDurationMs), "Interval duration must be positive.");
            IntervalDurationMs = intervalDurationMs;
        }
    }

  
    public class PatternTranslator
    {
        private readonly ExperimentConfiguration _config;

        public PatternTranslator(ExperimentConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            if (_config.MaxKeysPerIntervalKmax <= _config.MinKeysPerIntervalKmin)
            {
                throw new ArgumentException("Configuration error: MaxKeysPerIntervalKmax must be greater than MinKeysPerIntervalKmin.");
            }
        }

       
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
            int i = 1;
            foreach (int ind in keysPerInterval)
            {
                Debug.WriteLine($" {i} sample 1normalized to akp" + ind);
                i++;
            }
            return new KeystrokeStreamSchedule(keysPerInterval, _config.IntervalDurationT);
        }

        
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
                Debug.WriteLine($"{i + 1} akp to normalized for {pid}  " + ind);
            return new AbstractKeystrokePattern(outputSamples);
        }
    }
}