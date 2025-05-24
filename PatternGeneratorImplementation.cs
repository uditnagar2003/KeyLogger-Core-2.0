namespace VisualKeyloggerDetector.Core.PatternGeneration
{
    
    public interface IPatternGeneratorAlgorithm
    {
      
        List<double> GenerateSamples(int n);
    }

  
    public class RandomPatternAlgorithm : IPatternGeneratorAlgorithm
    {
        private readonly Random _random = new Random();

       
        public List<double> GenerateSamples(int n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), "Number of samples cannot be negative.");
            var samples = new List<double>(n);
            for (int i = 0; i < n; i++)
            {
                samples.Add(_random.NextDouble());
            }
            return samples;
        }
    }

    
    public class RandomFixedRangePatternAlgorithm : IPatternGeneratorAlgorithm
    {
        private readonly Random _random = new Random();

      
        public List<double> GenerateSamples(int n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), "Number of samples cannot be negative.");
            if (n == 0) return new List<double>();
            if (n == 1) return new List<double> { 0.5 }; // Single sample case

            // Generate samples uniformly distributed in [0, 1]
            var baseSamples = Enumerable.Range(0, n).Select(i => (double)i / (n - 1)).ToList();

            // Shuffle them randomly (Fisher-Yates shuffle)
            for (int i = baseSamples.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                // Swap using tuple deconstruction
                (baseSamples[i], baseSamples[j]) = (baseSamples[j], baseSamples[i]);
            }
            return baseSamples;
        }
    }

    
    public class ImpulsePatternAlgorithm : IPatternGeneratorAlgorithm
    {
        
        public List<double> GenerateSamples(int n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), "Number of samples cannot be negative.");
            var samples = new List<double>(n);
            for (int i = 0; i < n; i++)
            {
                samples.Add((i % 2 == 0) ? 0.0 : 1.0); // Alternating 0 and 1
            }
            return samples;
        }
    }

   
    public class SineWavePatternAlgorithm : IPatternGeneratorAlgorithm
    {
        
        public List<double> GenerateSamples(int n)
        {
            if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), "Number of samples cannot be negative.");
            if (n == 0) return new List<double>();
            if (n == 1) return new List<double> { 0.5 }; // Midpoint for single sample

            var samples = new List<double>(n);
            // Generate a full sine wave cycle scaled to [0, 1] over N samples
            for (int i = 0; i < n; i++)
            {
                // Use n instead of n-1 in denominator for smoother cycle if n is large? Let's stick to n-1 for full range.
                double sinValue = Math.Sin(2 * Math.PI * i / (n - 1)); // Value from -1 to 1
                samples.Add((sinValue + 1.0) / 2.0); // Scale to 0 to 1
            }
            return samples;
        }
    }
}