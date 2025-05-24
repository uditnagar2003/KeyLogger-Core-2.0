namespace VisualKeyloggerDetector.Core
{
   
    public class AbstractKeystrokePattern
    {
       
        public List<double> Samples { get; }

      
        /// <param name="samples">The list of normalized samples (expected range [0, 1]).</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="samples"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if any sample is outside the range [0, 1].</exception>
        public AbstractKeystrokePattern(List<double> samples)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            
            Samples = new List<double>(samples); 
        }

        public int Length => Samples.Count;
    }

    
    public class ExperimentConfiguration
    {
       
        public int PatternLengthN { get; set; } = 3;

      
        public int IntervalDurationT { get; set; } = 100;

        public int T { get; set; } = 1000;
       
        public int MinKeysPerIntervalKmin { get; set; } = 5;

        
        public int MaxKeysPerIntervalKmax { get; set; } = 10;

       
        public double DetectionThreshold { get; set; } = 0.7;

       
        public double MinAverageWriteBytesPerInterval { get; set; } = 200;

       
        public string ResultsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "detector_results_v2.txt");


        public List<ProcessInfoData> processInfoDatas = new List<ProcessInfoData>();

        public List<uint> ProcessIdsToMonitor = new List<uint>();

        public HashSet<string> SafeProcessNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Add other known safe processes
        };

        public List<string> ExcludedPathPrefixes { get; set; } = new List<string>
        {
            /*  @"C:\Windows\",
              @"C:\Program Files\",
              @"C:\Program Files (x86)\"
              // Add other known safe system/application directories
          */
        };
    }

   
    public class DetectionResult
    {
        
        public string ProcessName { get; set; }

      
        public uint ProcessId { get; set; }

       
        public string ExecutablePath { get; set; }

        public double Correlation { get; set; }

       
        public double AverageBytesWrittenPerInterval { get; set; }

       
        public double Threshold { get; set; }

        public bool IsDetected => !double.IsNaN(Correlation) && Correlation > Threshold;
    }
}