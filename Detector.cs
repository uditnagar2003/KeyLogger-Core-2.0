namespace VisualKeyloggerDetector.Core.Detection
{
    
    public class Detector
    {
       
        public double CalculatePCC(AbstractKeystrokePattern patternP, AbstractKeystrokePattern patternQ)
        {
            if (patternP == null) throw new ArgumentNullException(nameof(patternP));
            if (patternQ == null) throw new ArgumentNullException(nameof(patternQ));
            if (patternP.Length != patternQ.Length)
                throw new ArgumentException("Patterns must have the same length for PCC calculation.");

            int n = patternP.Length;
            // Need at least 2 data points to calculate correlation meaningfully.
            if (n < 2)
            {
                return double.NaN;
            }

            var samplesP = patternP.Samples;
            var samplesQ = patternQ.Samples;

            // Calculate means
            double meanP = 0.0;
            double meanQ = 0.0;
            for (int i = 0; i < n; i++)
            {
                meanP += samplesP[i];
                meanQ += samplesQ[i];
            }
            meanP /= n;
            meanQ /= n;


            // Calculate sum of squared deviations from mean and covariance
            double sumSqDevP = 0.0;
            double sumSqDevQ = 0.0;
            double sumCoDev = 0.0; // Sum for covariance calculation

            for (int i = 0; i < n; i++)
            {
                double devP = samplesP[i] - meanP;
                double devQ = samplesQ[i] - meanQ;
                sumSqDevP += devP * devP;
                sumSqDevQ += devQ * devQ;
                sumCoDev += devP * devQ;
            }

            // Check for zero standard deviation (constant patterns)
            // Use a small epsilon for robust floating-point comparison.
            const double epsilon = 1e-10;
            if (sumSqDevP < epsilon || sumSqDevQ < epsilon)
            {
                // If one or both patterns have no variance, correlation is undefined.
                // The paper suggests assigning 0 if the output is constant, but NaN is mathematically more correct.
                return double.NaN;
            }

            // Calculate standard deviations
            double stdDevP = Math.Sqrt(sumSqDevP);
            double stdDevQ = Math.Sqrt(sumSqDevQ);

            // Calculate PCC: cov(P,Q) / (stdDevP * stdDevQ)
            // Note: The sumCoDev is proportional to covariance. The N or N-1 factor cancels out in the PCC formula.
            double correlation = sumCoDev / (stdDevP * stdDevQ);
            Console.WriteLine($"PCC: {correlation}  stdDevP: {stdDevP}  stdDevQ: {stdDevQ}");

            // Clamp result to [-1, 1] due to potential floating point inaccuracies near the boundaries.
            return Math.Max(-1.0, Math.Min(1.0, correlation));
        }

      
        public bool ShouldTriggerDetection(double pccValue, double threshold)
        {
            // Check if PCC is valid (not NaN) and greater than the positive threshold.
            // The paper primarily focuses on positive correlation indicating the output follows the input.
            return !double.IsNaN(pccValue) && pccValue > threshold;
        }
    }
}