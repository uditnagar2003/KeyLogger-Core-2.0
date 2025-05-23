using System;
using VisualKeyloggerDetector.Core.PatternGeneration; // Needs interface

namespace VisualKeyloggerDetector.Core.PatternGeneration // Corrected namespace
{
    /// <summary>
    /// Generates Abstract Keystroke Patterns (AKP) using a specified algorithm.
    /// </summary>
    public class PatternGenerator
    {
        private readonly IPatternGeneratorAlgorithm _algorithm;

        /// <summary>
        /// Initializes a new instance of the <see cref="PatternGenerator"/> class.
        /// </summary>
        /// <param name="algorithm">The algorithm to use for generating pattern samples.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="algorithm"/> is null.</exception>
        public PatternGenerator(IPatternGeneratorAlgorithm algorithm)
        {
            _algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
        }

        /// <summary>
        /// Generates an Abstract Keystroke Pattern with the specified number of samples.
        /// </summary>
        /// <param name="n">The desired number of samples (length) for the pattern.</param>
        /// <returns>A new <see cref="AbstractKeystrokePattern"/> instance.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="n"/> is not positive.</exception>
        public AbstractKeystrokePattern GeneratePattern(int n)
        {
            if (n <= 0) throw new ArgumentOutOfRangeException(nameof(n), "Pattern length must be positive.");
            var samples = _algorithm.GenerateSamples(n);
            foreach (var num in samples)
            {
                Console.WriteLine($"Generated {num} sample{AlgorithmTypeName} algorithm.\n");
            }
            return new AbstractKeystrokePattern(samples);
        }

        /// <summary>
        /// Gets the type name of the underlying pattern generation algorithm being used.
        /// </summary>
        public string AlgorithmTypeName => _algorithm.GetType().Name;
        //Console.WriteLine($"Algorithm Type: {AlgorithmTypeName}");
    }
}