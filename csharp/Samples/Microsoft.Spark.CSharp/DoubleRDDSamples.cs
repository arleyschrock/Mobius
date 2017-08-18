// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;
using Microsoft.Spark.CSharp.Core;
using NUnit.Framework;

namespace Microsoft.Spark.CSharp.Samples
{
    class DoubleRDDSamples
    {
        [Sample]
        public static void DoubleRDDSumSample()
        {
            var sum = SparkCLRSamples.SparkContext.Parallelize(new double[] { 1.0, 2.0, 3.0 }, 2).Sum();
            Console.WriteLine(sum);

            if (SparkCLRSamples.Configuration.IsValidationEnabled)
            {
                Assert.AreEqual(6.0, sum);
            }
        }

        [Sample]
        public static void DoubleRDDStatsSample()
        {
            var stats = SparkCLRSamples.SparkContext.Parallelize(new double[] { 1, 2, 3 }, 2).Stats();
            Console.WriteLine(stats);

            if (SparkCLRSamples.Configuration.IsValidationEnabled)
            {
                Assert.AreEqual(6.0, stats.Sum);
                Assert.AreEqual(2.0, stats.Mean);
                Assert.AreEqual(3.0, stats.Max);
                Assert.AreEqual(1.0, stats.Min);
                Assert.AreEqual(0.6667, stats.Variance, 0.0001);
                Assert.AreEqual(1.0, stats.SampleVariance);
                Assert.AreEqual(0.8164, stats.Stdev, 0.0001);
                Assert.AreEqual(1.0, stats.SampleStdev);
            }
        }

        [Sample]
        public static void DoubleRDDMeanSample()
        {
            var mean = SparkCLRSamples.SparkContext.Parallelize(new double[] { 1, 2, 3 }, 2).Mean();
            Console.WriteLine(mean);

            if (SparkCLRSamples.Configuration.IsValidationEnabled)
            {
                Assert.AreEqual(2.0, mean);
            }
        }

        [Sample]
        public static void DoubleRDVarianceSample()
        {
            var variance = SparkCLRSamples.SparkContext.Parallelize(new double[] { 1, 2, 3 }, 2).Variance();
            Console.WriteLine(variance);

            if (SparkCLRSamples.Configuration.IsValidationEnabled)
            {
                Assert.AreEqual(0.6667, variance, 0.0001);
            }
        }

        [Sample]
        public static void DoubleRDDStdevSample()
        {
            var stdev = SparkCLRSamples.SparkContext.Parallelize(new double[] { 1, 2, 3 }, 2).Stdev();
            Console.WriteLine(stdev);

            if (SparkCLRSamples.Configuration.IsValidationEnabled)
            {
                Assert.AreEqual(0.8164, stdev, 0.0001);
            }
        }

        [Sample]
        public static void DoubleRDDSampleStdevSample()
        {
            var sampleStdev = SparkCLRSamples.SparkContext.Parallelize(new double[] { 1, 2, 3 }, 2).SampleStdev();
            Console.WriteLine(sampleStdev);

            if (SparkCLRSamples.Configuration.IsValidationEnabled)
            {
                Assert.AreEqual(1.0, sampleStdev);
            }
        }

        [Sample]
        public static void DoubleRDDSampleVarianceSample()
        {
            var sampleVariance = SparkCLRSamples.SparkContext.Parallelize(new double[] { 1, 2, 3 }, 2).SampleVariance();
            Console.WriteLine(sampleVariance);

            if (SparkCLRSamples.Configuration.IsValidationEnabled)
            {
                Assert.AreEqual(1.0, sampleVariance);
            }
        }
    }
}
