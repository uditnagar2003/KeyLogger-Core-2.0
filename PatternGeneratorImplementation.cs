namespace VisualKeyloggerDetector.Core.PatternGeneration
{
    /// <summary>
    /// Interface for algorithms that generate abstract patterns (sequences of normalized samples).
    /// </summary>
    public interface IPatternGeneratorAlgorithm
    {
        /// <summary>
        /// Generates a list of normalized samples representing a pattern.
        /// </summary>
        /// <param name="n">The number of samples required in the pattern.</param>
        /// <returns>A list of <paramref name="n"/> double values, typically between 0.0 and 1.0.</returns>
        List<double> GenerateSamples(int n);
    }

    // --- Algorithm Implementations ---

    /// <summary>
    /// Generates a pattern where each sample is a random double between 0.0 and 1.0.
    /// </summary>
    public class RandomPatternAlgorithm : IPatternGeneratorAlgorithm
    {
        private readonly Random _random = new Random();

        /// <summary>
        /// Generates a list of random samples.
        /// </summary>
        /// <param name="n">The number of samples required.</param>
        /// <returns>A list of <paramref name="n"/> random double values between 0.0 and 1.0.</returns>
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

    /// <summary>
    /// Generates a pattern by creating samples uniformly distributed between 0.0 and 1.0,
    /// and then randomly shuffling their order. This ensures maximum variability across the full range.
    /// </summary>
    public class RandomFixedRangePatternAlgorithm : IPatternGeneratorAlgorithm
    {
        private readonly Random _random = new Random();

        /// <summary>
        /// Generates a list of uniformly distributed samples in a random order.
        /// </summary>
        /// <param name="n">The number of samples required.</param>
        /// <returns>A list of <paramref name="n"/> double values, representing a permutation of samples evenly spaced between 0.0 and 1.0.</returns>
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

    /// <summary>
    /// Generates a pattern alternating between 0.0 and 1.0 (e.g., 0, 1, 0, 1, ...).
    /// This creates maximum variance between adjacent samples.
    /// </summary>
    public class ImpulsePatternAlgorithm : IPatternGeneratorAlgorithm
    {
        /// <summary>
        /// Generates a list of alternating 0.0 and 1.0 samples.
        /// </summary>
        /// <param name="n">The number of samples required.</param>
        /// <returns>A list of <paramref name="n"/> double values alternating between 0.0 and 1.0.</returns>
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

    /// <summary>
    /// Generates a pattern following a discrete sine wave oscillating between 0.0 and 1.0.
    /// </summary>
    public class SineWavePatternAlgorithm : IPatternGeneratorAlgorithm
    {
        /// <summary>
        /// Generates a list of samples forming a sine wave.
        /// </summary>
        /// <param name="n">The number of samples required.</param>
        /// <returns>A list of <paramref name="n"/> double values representing one cycle of a sine wave scaled to [0.0, 1.0].</returns>
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