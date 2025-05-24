using System.Diagnostics;

namespace VisualKeyloggerDetector.Core.PatternGeneration // Corrected namespace
{
   
    public class PatternGenerator
    {
        private readonly IPatternGeneratorAlgorithm _algorithm;

        public PatternGenerator(IPatternGeneratorAlgorithm algorithm)
        {
            _algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
        }

      
        public AbstractKeystrokePattern GeneratePattern(int n)
        {
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "Pattern length must be positive.");
            var samples = _algorithm.GenerateSamples(n);
            int i = 1;
            foreach (var num in samples)
            {
                Debug.WriteLine($"{i} Generated {num} sample{AlgorithmTypeName} algorithm.\n");
                i++;
            }
            return new AbstractKeystrokePattern(samples);
        }

        public string AlgorithmTypeName => _algorithm.GetType().Name;
        
    }
}