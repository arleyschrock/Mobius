﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Security.Cryptography;
using Microsoft.Spark.CSharp.Interop.Ipc;

namespace Microsoft.Spark.CSharp.Core
{
    /// <summary>
    /// operations only available to Tuple RDD
    /// 
    /// See also http://spark.apache.org/docs/latest/api/scala/index.html#org.apache.spark.rdd.PairRDDFunctions
    /// </summary>
    public static class PairRDDFunctions
    {
        /// <summary>
        /// Return the key-value pairs in this RDD to the master as a dictionary.
        ///
        /// var m = sc.Parallelize(new[] { new Tuple&lt;int, int>(1, 2), new Tuple&lt;int, int>(3, 4) }, 1).CollectAsMap()
        /// m[1]
        /// 2
        /// m[3]
        /// 4
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <returns></returns>
        public static IDictionary<K, V> CollectAsMap<K, V>(this RDD<Tuple<K, V>> self)
        {
            return self.Collect().ToDictionary(kv => kv.Item1, kv => kv.Item2);
        }

        /// <summary>
        /// Return an RDD with the keys of each tuple.
        ///
        /// >>> m = sc.Parallelize(new[] { new Tuple&lt;int, int>(1, 2), new Tuple&lt;int, int>(3, 4) }, 1).Keys().Collect()
        /// [1, 3]
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <returns></returns>
        public static RDD<K> Keys<K, V>(this RDD<Tuple<K, V>> self)
        {
            return self.Map<K>(kv => kv.Item1);
        }

        /// <summary>
        /// Return an RDD with the values of each tuple.
        ///
        /// >>> m = sc.Parallelize(new[] { new Tuple&lt;int, int>(1, 2), new Tuple&lt;int, int>(3, 4) }, 1).Values().Collect()
        /// [2, 4]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <returns></returns>
        public static RDD<V> Values<K, V>(this RDD<Tuple<K, V>> self)
        {
            return self.Map<V>(kv => kv.Item2);
        }

        /// <summary>
        /// Merge the values for each key using an associative reduce function.
        /// 
        /// This will also perform the merging locally on each mapper before
        /// sending results to a reducer, similarly to a "combiner" in MapReduce.
        /// 
        /// Output will be hash-partitioned with <paramref name="numPartitions"/> partitions, or
        /// the default parallelism level if <paramref name="numPartitions"/> is not specified.
        /// 
        /// sc.Parallelize(new[] 
        /// { 
        ///     new Tuple&lt;string, int>("a", 1), 
        ///     new Tuple&lt;string, int>("b", 1),
        ///     new Tuple&lt;string, int>("a", 1)
        /// }, 2)
        /// .ReduceByKey((x, y) => x + y).Collect()
        ///        
        /// [('a', 2), ('b', 1)]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <param name="reduceFunc"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, V>> ReduceByKey<K, V>(this RDD<Tuple<K, V>> self, Func<V, V, V> reduceFunc, int numPartitions = 0)
        {
            var locallyCombined = self.MapPartitionsWithIndex(new GroupByMergeHelper<K, V>(reduceFunc).Execute, true);

            var shuffled = locallyCombined.PartitionBy(numPartitions);

            return shuffled.MapPartitionsWithIndex(new GroupByMergeHelper<K, V>(reduceFunc).Execute, true);
        }

        /// <summary>
        /// Merge the values for each key using an associative reduce function, but
        /// return the results immediately to the master as a dictionary.
        /// 
        /// This will also perform the merging locally on each mapper before
        /// sending results to a reducer, similarly to a "combiner" in MapReduce.
        /// 
        /// sc.Parallelize(new[] 
        /// { 
        ///     new Tuple&lt;string, int>("a", 1), 
        ///     new Tuple&lt;string, int>("b", 1),
        ///     new Tuple&lt;string, int>("a", 1)
        /// }, 2)
        /// .ReduceByKeyLocally((x, y) => x + y).Collect()
        /// 
        /// [('a', 2), ('b', 1)]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <param name="reduceFunc"></param>
        /// <returns></returns>
        public static IDictionary<K, V> ReduceByKeyLocally<K, V>(this RDD<Tuple<K, V>> self, Func<V, V, V> reduceFunc)
        {
            return ReduceByKey(self, reduceFunc).CollectAsMap();
        }

        /// <summary>
        /// Count the number of elements for each key, and return the result to the master as a dictionary.
        /// 
        /// sc.Parallelize(new[] 
        /// { 
        ///     new Tuple&lt;string, int>("a", 1), 
        ///     new Tuple&lt;string, int>("b", 1),
        ///     new Tuple&lt;string, int>("a", 1)
        /// }, 2)
        /// .CountByKey((x, y) => x + y).Collect()
        /// 
        /// [('a', 2), ('b', 1)]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <returns></returns>
        public static IEnumerable<Tuple<K, long>> CountByKey<K, V>(this RDD<Tuple<K, V>> self)
        {
            return self.MapValues(v => 1L).ReduceByKey((a, b) => a + b).Collect();
        }

        /// <summary>
        /// Return an RDD containing all pairs of elements with matching keys in this RDD and <paramref name="other"/>.
        /// 
        /// Each pair of elements will be returned as a (k, (v1, v2)) tuple, where (k, v1) is in this RDD and (k, v2) is in <paramref name="other"/>.
        /// 
        /// Performs a hash join across the cluster.
        /// 
        /// var l = sc.Parallelize(
        ///     new[] { new Tuple&lt;string, int>("a", 1), new Tuple&lt;string, int>("b", 4) }, 1);
        /// var r = sc.Parallelize(
        ///     new[] { new Tuple&lt;string, int>("a", 2), new Tuple&lt;string, int>("a", 3) }, 1);
        /// var m = l.Join(r, 2).Collect();
        /// 
        /// [('a', (1, 2)), ('a', (1, 3))]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W"></typeparam>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, Tuple<V, W>>> Join<K, V, W>(
            this RDD<Tuple<K, V>> self,
            RDD<Tuple<K, W>> other,
            int numPartitions = 0)
        {
            return self.GroupWith(other, numPartitions).FlatMapValues(
                input => input.Item1.SelectMany(v => input.Item2.Select(w => new Tuple<V, W>(v, w)))
                );
        }

        /// <summary>
        /// Perform a left outer join of this RDD and <paramref name="other"/>.
        /// 
        /// For each element (k, v) in this RDD, the resulting RDD will either
        /// contain all pairs (k, (v, Option)) for w in <paramref name="other"/>, where Option.IsDefined is TRUE, or the pair
        /// (k, (v, Option)) if no elements in <paramref name="other"/> have key k, where Option.IsDefined is FALSE. 
        /// 
        /// Hash-partitions the resulting RDD into the given number of partitions.
        /// 
        /// var l = sc.Parallelize(
        ///     new[] { new Tuple&lt;string, int>("a", 1), new Tuple&lt;string, int>("b", 4) }, 1);
        /// var r = sc.Parallelize(
        ///     new[] { new Tuple&lt;string, int>("a", 2) }, 1);
        /// var m = l.LeftOuterJoin(r).Collect();
        /// 
        /// [('a', (1, 2)), ('b', (4, Option))]
        /// * Option.IsDefined = FALSE
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W"></typeparam>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, Tuple<V, Option<W>>>> LeftOuterJoin<K, V, W>(
            this RDD<Tuple<K, V>> self,
            RDD<Tuple<K, W>> other,
            int numPartitions = 0)
        {
            return self.GroupWith(other, numPartitions).FlatMapValues(
                input => input.Item1.SelectMany(v => input.Item2.NullIfEmpty().Select(optionW => new Tuple<V, Option<W>>(v, optionW)))                );
        }

        /// <summary>
        /// Perform a right outer join of this RDD and <paramref name="other"/>.
        /// 
        /// For each element (k, w) in <paramref name="other"/>, the resulting RDD will either
        /// contain all pairs (k, (Option, w)) for v in this, where Option.IsDefined is TRUE, or the pair (k, (Option, w))
        /// if no elements in this RDD have key k, where Option.IsDefined is FALSE.
        /// 
        /// Hash-partitions the resulting RDD into the given number of partitions.
        /// 
        /// var l = sc.Parallelize(
        ///     new[] { new Tuple&lt;string, int>("a", 2) }, 1);
        /// var r = sc.Parallelize(
        ///     new[] { new Tuple&lt;string, int>("a", 1), new Tuple&lt;string, int>("b", 4) }, 1);
        /// var m = l.RightOuterJoin(r).Collect();
        /// 
        /// [('a', (2, 1)), ('b', (Option, 4))]
        /// * Option.IsDefined = FALSE
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W"></typeparam>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, Tuple<Option<V>, W>>> RightOuterJoin<K, V, W>(
            this RDD<Tuple<K, V>> self,
            RDD<Tuple<K, W>> other,
            int numPartitions = 0)
        {
            return self.GroupWith(other, numPartitions).FlatMapValues(
                input => input.Item1.NullIfEmpty().SelectMany(v => input.Item2.Select(w => new Tuple<Option<V>, W>(v, w)))
                );
        }

        /// <summary>
        /// Perform a full outer join of this RDD and <paramref name="other"/>.
        /// 
        /// For each element (k, v) in this RDD, the resulting RDD will either
        /// contain all pairs (k, (v, w)) for w in <paramref name="other"/>, or the pair
        /// (k, (v, None)) if no elements in <paramref name="other"/> have key k.
        /// 
        /// Similarly, for each element (k, w) in <paramref name="other"/>, the resulting RDD will
        /// either contain all pairs (k, (v, w)) for v in this RDD, or the pair
        /// (k, (None, w)) if no elements in this RDD have key k.
        /// 
        /// Hash-partitions the resulting RDD into the given number of partitions.
        /// 
        /// var l = sc.Parallelize(
        ///     new[] { new Tuple&lt;string, int>("a", 1), Tuple&lt;string, int>("b", 4) }, 1);
        /// var r = sc.Parallelize(
        ///     new[] { new Tuple&lt;string, int>("a", 2), new Tuple&lt;string, int>("c", 8) }, 1);
        /// var m = l.FullOuterJoin(r).Collect();
        /// 
        /// [('a', (1, 2)), ('b', (4, None)), ('c', (None, 8))]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W"></typeparam>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, Tuple<Option<V>, Option<W>>>> FullOuterJoin<K, V, W>(
            this RDD<Tuple<K, V>> self,
            RDD<Tuple<K, W>> other,
            int numPartitions = 0)
        {
            return self.GroupWith(other, numPartitions).FlatMapValues(
                    input => input.Item1.NullIfEmpty().SelectMany(v => input.Item2.NullIfEmpty().Select(w => new Tuple<Option<V>, Option<W>>(v, w)))
                );
        }

        /// <summary>
        /// Return a copy of the RDD partitioned using the specified partitioner.
        /// 
        /// sc.Parallelize(new[] { 1, 2, 3, 4, 2, 4, 1 }, 1).Map(x => new Tuple&lt;int, int>(x, x)).PartitionBy(3).Glom().Collect()
        /// </summary>
        /// <param name="self"></param>
        /// <param name="numPartitions"></param>
        /// <param name="partitionFunc"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, V>> PartitionBy<K, V>(this RDD<Tuple<K, V>> self, int numPartitions = 0, 
            Func<dynamic, int> partitionFunc = null)
        {
            if (numPartitions == 0)
            {
                numPartitions = self.GetDefaultPartitionNum();
            }
            
            var partitioner = new Partitioner(numPartitions, partitionFunc);
            if (self.partitioner != null && self.partitioner.Equals(partitioner))
                return self;

            var keyed = self.MapPartitionsWithIndex(new AddShuffleKeyHelper<K, V>(numPartitions, partitionFunc).Execute, true);
            keyed.bypassSerializer = true;
            // convert shuffling version of RDD[(Long, Array[Byte])] back to normal RDD[Array[Byte]]
            // invoking property keyed.RddProxy marks the end of current pipeline RDD after shuffling
            // and potentially starts next pipeline RDD with defult SerializedMode.Byte
            var rdd = new RDD<Tuple<K, V>>(self.sparkContext.SparkContextProxy.CreatePairwiseRDD(keyed.RddProxy, numPartitions,
                GenerateObjectId(partitionFunc)), self.sparkContext);
            rdd.partitioner = partitioner;

            return rdd;
        }

        /// <summary>
        /// # TODO: add control over map-side aggregation
        /// Generic function to combine the elements for each key using a custom
        /// set of aggregation functions.
        /// 
        /// Turns an RDD[(K, V)] into a result of type RDD[(K, C)], for a "combined
        /// type" C.  Note that V and C can be different -- for example, one might
        /// group an RDD of type (Int, Int) into an RDD of type (Int, List[Int]).
        /// 
        /// Users provide three functions:
        /// 
        ///     - <paramref name="createCombiner"/>, which turns a V into a C (e.g., creates a one-element list)
        ///     - <paramref name="mergeValue"/>, to merge a V into a C (e.g., adds it to the end of
        ///       a list)
        ///     - <paramref name="mergeCombiners"/>, to combine two C's into a single one.
        /// 
        /// In addition, users can control the partitioning of the output RDD.
        /// 
        /// sc.Parallelize(
        ///         new[] 
        ///         { 
        ///             new Tuple&lt;string, int>("a", 1), 
        ///             new Tuple&lt;string, int>("b", 1),
        ///             new Tuple&lt;string, int>("a", 1)
        ///         }, 2)
        ///         .CombineByKey(() => string.Empty, (x, y) => x + y.ToString(), (x, y) => x + y).Collect()
        ///         
        /// [('a', '11'), ('b', '1')]
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="C"></typeparam>
        /// <param name="self"></param>
        /// <param name="createCombiner"></param>
        /// <param name="mergeValue"></param>
        /// <param name="mergeCombiners"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, C>> CombineByKey<K, V, C>(
            this RDD<Tuple<K, V>> self,
            Func<C> createCombiner,
            Func<C, V, C> mergeValue,
            Func<C, C, C> mergeCombiners,
            int numPartitions = 0)
        {
            var locallyCombined = self.MapPartitionsWithIndex(new GroupByCombineHelper<K, V, C>(createCombiner, mergeValue).Execute, true);

            var shuffled = locallyCombined.PartitionBy(numPartitions);

            return shuffled.MapPartitionsWithIndex(new GroupByMergeHelper<K, C>(mergeCombiners).Execute, true);
        }

        /// <summary>
        /// Aggregate the values of each key, using given combine functions and a neutral
        /// "zero value". This function can return a different result type, U, than the type
        /// of the values in this RDD, V. Thus, we need one operation for merging a V into
        /// a U and one operation for merging two U's, The former operation is used for merging
        /// values within a partition, and the latter is used for merging values between
        /// partitions. To avoid memory allocation, both of these functions are
        /// allowed to modify and return their first argument instead of creating a new U.
        /// 
        /// sc.Parallelize(
        ///         new[] 
        ///         { 
        ///             new Tuple&lt;string, int>("a", 1), 
        ///             new Tuple&lt;string, int>("b", 1),
        ///             new Tuple&lt;string, int>("a", 1)
        ///         }, 2)
        ///         .CombineByKey(() => string.Empty, (x, y) => x + y.ToString(), (x, y) => x + y).Collect()
        ///         
        /// [('a', 2), ('b', 1)]
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="self"></param>
        /// <param name="zeroValue"></param>
        /// <param name="seqOp"></param>
        /// <param name="combOp"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, U>> AggregateByKey<K, V, U>(
            this RDD<Tuple<K, V>> self,
            Func<U> zeroValue,
            Func<U, V, U> seqOp,
            Func<U, U, U> combOp,
            int numPartitions = 0)
        {
            return self.CombineByKey(zeroValue, seqOp, combOp, numPartitions);
        }

        /// <summary>
        /// Merge the values for each key using an associative function "func"
        /// and a neutral "zeroValue" which may be added to the result an
        /// arbitrary number of times, and must not change the result
        /// (e.g., 0 for addition, or 1 for multiplication.).
        /// 
        /// sc.Parallelize(
        ///         new[] 
        ///         { 
        ///             new Tuple&lt;string, int>("a", 1), 
        ///             new Tuple&lt;string, int>("b", 1),
        ///             new Tuple&lt;string, int>("a", 1)
        ///         }, 2)
        ///         .CombineByKey(() => string.Empty, (x, y) => x + y.ToString(), (x, y) => x + y).Collect()
        ///         
        /// [('a', 2), ('b', 1)]
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <param name="zeroValue"></param>
        /// <param name="func"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, V>> FoldByKey<K, V>(
            this RDD<Tuple<K, V>> self,
            Func<V> zeroValue,
            Func<V, V, V> func,
            int numPartitions = 0)
        {
            return self.CombineByKey(zeroValue, func, func, numPartitions);
        }

        /// <summary>
        /// Group the values for each key in the RDD into a single sequence.
        /// Hash-partitions the resulting RDD with numPartitions partitions.
        /// 
        /// Note: If you are grouping in order to perform an aggregation (such as a
        /// sum or average) over each key, using reduceByKey or aggregateByKey will
        /// provide much better performance.
        /// 
        /// sc.Parallelize(
        ///         new[] 
        ///         { 
        ///             new Tuple&lt;string, int>("a", 1), 
        ///             new Tuple&lt;string, int>("b", 1),
        ///             new Tuple&lt;string, int>("a", 1)
        ///         }, 2)
        ///         .GroupByKey().MapValues(l => string.Join(" ", l)).Collect()
        ///         
        /// [('a', [1, 1]), ('b', [1])]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, List<V>>> GroupByKey<K, V>(this RDD<Tuple<K, V>> self, int numPartitions = 0)
        {
            return CombineByKey(self,
                () => new List<V>(),
                (c, v) => { c.Add(v); return c; },
                (c1, c2) => { c1.AddRange(c2); return c1; },
                numPartitions);
        }

        /// <summary>
        /// Pass each value in the key-value pair RDD through a map function
        /// without changing the keys; this also retains the original RDD's partitioning.
        /// 
        /// sc.Parallelize(
        ///         new[] 
        ///         { 
        ///             new Tuple&lt;string, string[]>("a", new[]{"apple", "banana", "lemon"}), 
        ///             new Tuple&lt;string, string[]>("b", new[]{"grapes"})
        ///         }, 2)
        ///         .MapValues(x => x.Length).Collect()
        ///         
        /// [('a', 3), ('b', 1)]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="self"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, U>> MapValues<K, V, U>(this RDD<Tuple<K, V>> self, Func<V, U> func)
        {
            return self.Map(new MapValuesHelper<K, V, U>(func).Execute, true);
        }

        /// <summary>
        /// Pass each value in the key-value pair RDD through a flatMap function
        /// without changing the keys; this also retains the original RDD's partitioning.
        /// 
        /// x = sc.Parallelize(
        ///         new[] 
        ///         { 
        ///             new Tuple&lt;string, string[]>("a", new[]{"x", "y", "z"}), 
        ///             new Tuple&lt;string, string[]>("b", new[]{"p", "r"})
        ///         }, 2)
        ///         .FlatMapValues(x => x).Collect()
        ///         
        /// [('a', 'x'), ('a', 'y'), ('a', 'z'), ('b', 'p'), ('b', 'r')]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="U"></typeparam>
        /// <param name="self"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, U>> FlatMapValues<K, V, U>(this RDD<Tuple<K, V>> self, Func<V, IEnumerable<U>> func)
        {
            return self.FlatMap(new FlatMapValuesHelper<K, V, U>(func).Execute, true);
        }

        /// <summary>
        /// explicitly convert Tuple&lt;K, V> to Tuple&lt;K, dynamic>
        /// since they are incompatibles types unlike V to dynamic
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W1"></typeparam>
        /// <typeparam name="W2"></typeparam>
        /// <typeparam name="W3"></typeparam>
        /// <param name="self"></param>
        /// <returns></returns>
        private static RDD<Tuple<K, dynamic>> MapPartitionsWithIndex<K, V, W1, W2, W3>(this RDD<Tuple<K, dynamic>> self)
        {
            CSharpWorkerFunc csharpWorkerFunc = new CSharpWorkerFunc(new DynamicTypingWrapper<K, V, W1, W2, W3>().Execute);
            var pipelinedRDD = new PipelinedRDD<Tuple<K, dynamic>>
            {
                workerFunc = csharpWorkerFunc,
                preservesPartitioning = true,
                previousRddProxy = self.rddProxy,
                prevSerializedMode = self.serializedMode,

                sparkContext = self.sparkContext,
                rddProxy = null,
                serializedMode = SerializedMode.Byte,
                partitioner = self.partitioner
            };
            return pipelinedRDD;
        }

        /// <summary>
        /// For each key k in this RDD or <paramref name="other"/>, return a resulting RDD that
        /// contains a tuple with the list of values for that key in this RDD as well as <paramref name="other"/>.
        /// 
        /// var x = sc.Parallelize(new[] { new Tuple&lt;string, int>("a", 1), new Tuple&lt;string, int>("b", 4) }, 2);
        /// var y = sc.Parallelize(new[] { new Tuple&lt;string, int>("a", 2) }, 1);
        /// x.GroupWith(y).Collect();
        /// 
        /// [('a', ([1], [2])), ('b', ([4], []))]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W"></typeparam>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, Tuple<List<V>, List<W>>>> GroupWith<K, V, W>(
            this RDD<Tuple<K, V>> self,
            RDD<Tuple<K, W>> other,
            int numPartitions = 0)
        {
            // MapValues, which introduces extra CSharpRDD, is not necessary when union different RDD types
            if (typeof(V) != typeof(W))
            {
                return self.ConvertTo<Tuple<K, dynamic>>()
                    .Union(other.ConvertTo<Tuple<K, dynamic>>())
                    .MapPartitionsWithIndex<K, V, W, W, W>()
                    .CombineByKey(
                    () => new Tuple<List<V>, List<W>>(new List<V>(), new List<W>()),
                    (c, v) => { if (v is V) c.Item1.Add((V)v); else c.Item2.Add((W)v); return c; },
                    (c1, c2) => { c1.Item1.AddRange(c2.Item1); c1.Item2.AddRange(c2.Item2); return c1; },
                    numPartitions);
            }

            return self.MapValues(v => new Tuple<int, dynamic>(0, v))
                .Union(other.MapValues(w => new Tuple<int, dynamic>(1, w)))
                .CombineByKey(
                () => new Tuple<List<V>, List<W>>(new List<V>(), new List<W>()),
                (c, v) => { if (v.Item1 == 0) c.Item1.Add((V)v.Item2); else c.Item2.Add((W)v.Item2); return c; },
                (c1, c2) => { c1.Item1.AddRange(c2.Item1); c1.Item2.AddRange(c2.Item2); return c1; },
                numPartitions);
        }

        /// <summary>
        /// var x = sc.Parallelize(new[] { new Tuple&lt;string, int>("a", 5), new Tuple&lt;string, int>("b", 6) }, 2);
        /// var y = sc.Parallelize(new[] { new Tuple&lt;string, int>("a", 1), new Tuple&lt;string, int>("b", 4) }, 2);
        /// var z = sc.Parallelize(new[] { new Tuple&lt;string, int>("a", 2) }, 1);
        /// x.GroupWith(y, z).Collect();
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W1"></typeparam>
        /// <typeparam name="W2"></typeparam>
        /// <param name="self"></param>
        /// <param name="other1"></param>
        /// <param name="other2"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, Tuple<List<V>, List<W1>, List<W2>>>> GroupWith<K, V, W1, W2>(
            this RDD<Tuple<K, V>> self,
            RDD<Tuple<K, W1>> other1,
            RDD<Tuple<K, W2>> other2,
            int numPartitions = 0)
        {
            // MapValues, which introduces extra CSharpRDD, is not necessary when union different RDD types
            if (!(typeof(V) == typeof(W1) && typeof(V) == typeof(W2)))
            {
                return self.ConvertTo<Tuple<K, dynamic>>()
                    .Union(other1.ConvertTo<Tuple<K, dynamic>>())
                    .Union(other2.ConvertTo<Tuple<K, dynamic>>())
                    .MapPartitionsWithIndex<K, V, W1, W2, W2>()
                    .CombineByKey(
                    () => new Tuple<List<V>, List<W1>, List<W2>>(new List<V>(), new List<W1>(), new List<W2>()),
                    (c, v) => { if (v is V) c.Item1.Add((V)v); else if (v is W1) c.Item2.Add((W1)v); else c.Item3.Add((W2)v); return c; },
                    (c1, c2) => { c1.Item1.AddRange(c2.Item1); c1.Item2.AddRange(c2.Item2); c1.Item3.AddRange(c2.Item3); return c1; },
                    numPartitions);
            }

            return self.MapValues(v => new Tuple<int, dynamic>(0, v))
                .Union(other1.MapValues(w1 => new Tuple<int, dynamic>(1, w1)))
                .Union(other2.MapValues(w2 => new Tuple<int, dynamic>(2, w2)))
                .CombineByKey(
                () => new Tuple<List<V>, List<W1>, List<W2>>(new List<V>(), new List<W1>(), new List<W2>()),
                (c, v) => { if (v.Item1 == 0) c.Item1.Add((V)v.Item2); else if (v.Item1 == 1) c.Item2.Add((W1)v.Item2); else c.Item3.Add((W2)v.Item2); return c; },
                (c1, c2) => { c1.Item1.AddRange(c2.Item1); c1.Item2.AddRange(c2.Item2); c1.Item3.AddRange(c2.Item3); return c1; },
                numPartitions);
        }

        /// <summary>
        /// var x = sc.Parallelize(new[] { new Tuple&lt;string, int>("a", 5), new Tuple&lt;string, int>("b", 6) }, 2);
        /// var y = sc.Parallelize(new[] { new Tuple&lt;string, int>("a", 1), new Tuple&lt;string, int>("b", 4) }, 2);
        /// var z = sc.Parallelize(new[] { new Tuple&lt;string, int>("a", 2) }, 1);
        /// var w = sc.Parallelize(new[] { new Tuple&lt;string, int>("b", 42) }, 1);
        /// var m = x.GroupWith(y, z, w).MapValues(l => string.Join(" ", l.Item1) + " : " + string.Join(" ", l.Item2) + " : " + string.Join(" ", l.Item3) + " : " + string.Join(" ", l.Item4)).Collect();
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W1"></typeparam>
        /// <typeparam name="W2"></typeparam>
        /// <typeparam name="W3"></typeparam>
        /// <param name="self"></param>
        /// <param name="other1"></param>
        /// <param name="other2"></param>
        /// <param name="other3"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, Tuple<List<V>, List<W1>, List<W2>, List<W3>>>> GroupWith<K, V, W1, W2, W3>(
            this RDD<Tuple<K, V>> self,
            RDD<Tuple<K, W1>> other1,
            RDD<Tuple<K, W2>> other2,
            RDD<Tuple<K, W3>> other3,
            int numPartitions = 0)
        {
            // MapValues, which introduces extra CSharpRDD, is not necessary when union different RDD types
            if (!(typeof(V) == typeof(W1) && typeof(V) == typeof(W2)))
            {
                return self.ConvertTo<Tuple<K, dynamic>>()
                    .Union(other1.ConvertTo<Tuple<K, dynamic>>())
                    .Union(other2.ConvertTo<Tuple<K, dynamic>>())
                    .Union(other3.ConvertTo<Tuple<K, dynamic>>())
                    .MapPartitionsWithIndex<K, V, W1, W2, W3>()
                    .CombineByKey(
                    () => new Tuple<List<V>, List<W1>, List<W2>, List<W3>>(new List<V>(), new List<W1>(), new List<W2>(), new List<W3>()),
                    (c, v) => { if (v is V) c.Item1.Add((V)v); else if (v is W1) c.Item2.Add((W1)v); else if (v is W2) c.Item3.Add((W2)v); else c.Item4.Add((W3)v); return c; },
                    (c1, c2) => { c1.Item1.AddRange(c2.Item1); c1.Item2.AddRange(c2.Item2); c1.Item3.AddRange(c2.Item3); c1.Item4.AddRange(c2.Item4); return c1; },
                    numPartitions);
            }

            return self.MapValues(v => new Tuple<int, dynamic>(0, v))
                .Union(other1.MapValues(w1 => new Tuple<int, dynamic>(1, w1)))
                .Union(other2.MapValues(w2 => new Tuple<int, dynamic>(2, w2)))
                .Union(other3.MapValues(w3 => new Tuple<int, dynamic>(3, w3)))
                .CombineByKey(
                () => new Tuple<List<V>, List<W1>, List<W2>, List<W3>>(new List<V>(), new List<W1>(), new List<W2>(), new List<W3>()),
                (c, v) => { if (v.Item1 == 0) c.Item1.Add((V)v.Item2); else if (v.Item1 == 1) c.Item2.Add((W1)v.Item2); else if (v.Item1 == 2) c.Item3.Add((W2)v.Item2); else c.Item4.Add((W3)v.Item2); return c; },
                (c1, c2) => { c1.Item1.AddRange(c2.Item1); c1.Item2.AddRange(c2.Item2); c1.Item3.AddRange(c2.Item3); c1.Item4.AddRange(c2.Item4); return c1; },
                numPartitions);
        }

        // /// <summary>
        // /// TO DO: C# version of RDDSampler.py
        // /// Return a subset of this RDD sampled by key (via stratified sampling).
        // /// Create a sample of this RDD using variable sampling rates for
        // /// different keys as specified by fractions, a key to sampling rate map.
        // /// 
        // /// var fractions = new <see cref="Dictionary{string, double}"/> { { "a", 0.2 }, { "b", 0.1 } };
        // /// var rdd = sc.Parallelize(fractions.Keys.ToArray(), 2).Cartesian(sc.Parallelize(Enumerable.Range(0, 1000), 2));
        // /// var sample = rdd.Map(t => new Tuple&lt;string, int>(t.Item1, t.Item2)).SampleByKey(false, fractions, 2).GroupByKey().Collect();
        // /// 
        // /// 100 &lt; sample["a"].Length &lt; 300 and 50 &lt; sample["b"].Length &lt; 150
        // /// true
        // /// max(sample["a"]) &lt;= 999 and min(sample["a"]) >= 0
        // /// true
        // /// max(sample["b"]) &lt;= 999 and min(sample["b"]) >= 0
        // /// true
        // /// 
        // /// </summary>
        // /// <typeparam name="K"></typeparam>
        // /// <typeparam name="V"></typeparam>
        // /// <param name="self"></param>
        // /// <param name="withReplacement"></param>
        // /// <param name="fractions"></param>
        // /// <param name="seed"></param>
        // /// <returns></returns>
        //public static RDD<Tuple<string, V>> SampleByKey<V>(
        //    this RDD<Tuple<string, V>> self,
        //    bool withReplacement,
        //    Dictionary<string, double> fractions,
        //    long seed)
        //{
        //    if (fractions.Any(f => f.Value < 0.0))
        //        throw new ArgumentException(string.Format("Negative fraction value found in: {0}", string.Join(",", fractions.Values.ToArray())));

        //    return new RDD<Tuple<string, V>>(self.RddProxy.SampleByKey(withReplacement, fractions, seed), self.sparkContext);
        //}

        /// <summary>
        /// Return each (key, value) pair in this RDD that has no pair with matching key in <paramref name="other"/>.
        /// 
        /// var x = sc.Parallelize(new[] { new Tuple&lt;string, int?>("a", 1), new Tuple&lt;string, int?>("b", 4), new Tuple&lt;string, int?>("b", 5), new Tuple&lt;string, int?>("a", 2) }, 2);
        /// var y = sc.Parallelize(new[] { new Tuple&lt;string, int?>("a", 3), new Tuple&lt;string, int?>("c", null) }, 2);
        /// x.SubtractByKey(y).Collect();
        /// 
        /// [('b', 4), ('b', 5)]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <typeparam name="W"></typeparam>
        /// <param name="self"></param>
        /// <param name="other"></param>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public static RDD<Tuple<K, V>> SubtractByKey<K, V, W>(this RDD<Tuple<K, V>> self, RDD<Tuple<K, W>> other, int numPartitions = 0)
        {
            return self.GroupWith(other, numPartitions).FlatMapValues(t => t.Item1.Where(v => t.Item2.Count == 0));
        }

        /// <summary>
        /// Return the list of values in the RDD for key `key`. This operation
        /// is done efficiently if the RDD has a known partitioner by only
        /// searching the partition that the key maps to.
        /// 
        /// >>> l = range(1000)
        /// >>> rdd = sc.Parallelize(Enumerable.Range(0, 1000).Zip(Enumerable.Range(0, 1000), (x, y) => new Tuple&lt;int, int>(x, y)), 10)
        /// >>> rdd.lookup(42)
        /// [42]
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static V[] Lookup<K, V>(this RDD<Tuple<K, V>> self, K key)
        {
            return self.Filter(new LookupHelper<K, V>(key).Execute).Values().Collect();
        }

        /// <summary>
        /// Output a Python RDD of key-value pairs (of form RDD[(K, V)]) to any Hadoop file
        /// system, using the new Hadoop OutputFormat API (mapreduce package). Keys/values are
        /// converted for output using either user specified converters or, by default,
        /// org.apache.spark.api.python.JavaToWritableConverter.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <param name="conf">Hadoop job configuration, passed in as a dict</param>
        public static void SaveAsNewAPIHadoopDataset<K, V>(this RDD<Tuple<K, V>> self, IEnumerable<Tuple<string, string>> conf)
        {
            self.RddProxy.SaveAsNewAPIHadoopDataset(conf);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <param name="path">path to Hadoop file</param>
        /// <param name="outputFormatClass">fully qualified classname of Hadoop OutputFormat (e.g. "org.apache.hadoop.mapreduce.lib.output.SequenceFileOutputFormat")</param>
        /// <param name="keyClass">fully qualified classname of key Writable class (e.g. "org.apache.hadoop.io.IntWritable", None by default)</param>
        /// <param name="valueClass">fully qualified classname of value Writable class (e.g. "org.apache.hadoop.io.Text", None by default)</param>
        /// <param name="conf">Hadoop job configuration, passed in as a dict (None by default)</param>
        public static void SaveAsNewAPIHadoopFile<K, V>(this RDD<Tuple<K, V>> self, string path, string outputFormatClass, string keyClass, string valueClass, IEnumerable<Tuple<string, string>> conf)
        {
            self.RddProxy.SaveAsNewAPIHadoopFile(path, outputFormatClass, keyClass, valueClass, conf);
        }

        /// <summary>
        /// Output a Python RDD of key-value pairs (of form RDD[(K, V)]) to any Hadoop file
        /// system, using the old Hadoop OutputFormat API (mapred package). Keys/values are
        /// converted for output using either user specified converters or, by default,
        /// org.apache.spark.api.python.JavaToWritableConverter.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <param name="conf">Hadoop job configuration, passed in as a dict</param>
        public static void SaveAsHadoopDataset<K, V>(this RDD<Tuple<K, V>> self, IEnumerable<Tuple<string, string>> conf)
        {
            self.RddProxy.SaveAsHadoopDataset(conf);
        }

        /// <summary>
        /// Output a Python RDD of key-value pairs (of form RDD[(K, V)]) to any Hadoop file
        /// system, using the old Hadoop OutputFormat API (mapred package). Key and value types
        /// will be inferred if not specified. Keys and values are converted for output using either
        /// user specified converters or org.apache.spark.api.python.JavaToWritableConverter. The
        /// <paramref name="conf"/> is applied on top of the base Hadoop conf associated with the SparkContext
        /// of this RDD to create a merged Hadoop MapReduce job configuration for saving the data.
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <param name="path">path to Hadoop file</param>
        /// <param name="outputFormatClass">fully qualified classname of Hadoop OutputFormat (e.g. "org.apache.hadoop.mapred.SequenceFileOutputFormat")</param>
        /// <param name="keyClass">fully qualified classname of key Writable class (e.g. "org.apache.hadoop.io.IntWritable", None by default)</param>
        /// <param name="valueClass">fully qualified classname of value Writable class (e.g. "org.apache.hadoop.io.Text", None by default)</param>
        /// <param name="conf">(None by default)</param>
        /// <param name="compressionCodecClass">(None by default)</param>
        public static void SaveAsHadoopFile<K, V>(this RDD<Tuple<K, V>> self, string path, string outputFormatClass, string keyClass, string valueClass, IEnumerable<Tuple<string, string>> conf, string compressionCodecClass)
        {
            self.RddProxy.SaveAsHadoopFile(path, outputFormatClass, keyClass, valueClass, conf, compressionCodecClass);
        }

        /// <summary>
        /// Output a Python RDD of key-value pairs (of form RDD[(K, V)]) to any Hadoop file
        /// system, using the org.apache.hadoop.io.Writable types that we convert from the
        /// RDD's key and value types. The mechanism is as follows:
        /// 
        ///     1. Pyrolite is used to convert pickled Python RDD into RDD of Java objects.
        ///     2. Keys and values of this Java RDD are converted to Writables and written out.
        /// 
        /// </summary>
        /// <typeparam name="K"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="self"></param>
        /// <param name="path">path to sequence file</param>
        /// <param name="compressionCodecClass">(None by default)</param>
        public static void SaveAsSequenceFile<K, V>(this RDD<Tuple<K, V>> self, string path, string compressionCodecClass)
        {
            self.RddProxy.SaveAsSequenceFile(path, compressionCodecClass);
        }

        /// <summary>
        /// These classes are defined explicitly and marked as [Serializable]instead of using anonymous method as delegate to 
        /// prevent C# compiler from generating private anonymous type that is not serializable. Since the delegate has to be 
        /// serialized and sent to the Spark workers for execution, it is necessary to have the type marked [Serializable]. 
        /// These classes are to work around the limitation on the serializability of compiler generated types
        /// </summary>
        [Serializable]
        private class GroupByMergeHelper<K, C>
        {
            private readonly Func<C, C, C> mergeCombiners;
            public GroupByMergeHelper(Func<C, C, C> mc)
            {
                mergeCombiners = mc;
            }

            public IEnumerable<Tuple<K, C>> Execute(int pid, IEnumerable<Tuple<K, C>> input)
            {
                return input.GroupBy(
                    kvp => kvp.Item1,
                    kvp => kvp.Item2,
                    (k, v) => new Tuple<K, C>(k, v.Aggregate(mergeCombiners))
                    );
            }
        }

        [Serializable]
        private class GroupByCombineHelper<K, V, C>
        {
            private readonly Func<C> createCombiner;
            private readonly Func<C, V, C> mergeValue;
            public GroupByCombineHelper(Func<C> createCombiner, Func<C, V, C> mergeValue)
            {
                this.createCombiner = createCombiner;
                this.mergeValue = mergeValue;
            }

            public IEnumerable<Tuple<K, C>> Execute(int pid, IEnumerable<Tuple<K, V>> input)
            {
                return input.GroupBy(
                    kvp => kvp.Item1,
                    kvp => kvp.Item2,
                    (k, v) => new Tuple<K, C>(k, v.Aggregate(createCombiner(), mergeValue))
                    );
            }
        }
        
        [Serializable]
        public class AddShuffleKeyHelper<K, V>
        {
            [NonSerialized]
            private MD5 md5 = MD5.Create();
            private readonly int numPartitions;
            private readonly Func<dynamic, int> partitionFunc = null;

            public AddShuffleKeyHelper(int numPartitions, Func<dynamic, int> partitionFunc = null)
            {
                this.numPartitions = numPartitions;
                this.partitionFunc = partitionFunc;
            }

            public IEnumerable<byte[]> Execute(int split, IEnumerable<Tuple<K, V>> input)
            {
                // make sure that md5 is not null even if it is deseriazed in C# worker
                if (md5 == null)
                {
                    md5 = MD5.Create();
                }
                IFormatter formatter = new BinaryFormatter();
                foreach (var kv in input)
                {
                    var ms = new MemoryStream();
                    if (partitionFunc == null)
                    {
                        formatter.Serialize(ms, kv.Item1);
                        yield return md5.ComputeHash(ms.ToArray()).Take(8).ToArray();
                    }
                    else
                    {
                        long pid = (long)(partitionFunc(kv.Item1) % numPartitions);
                        yield return SerDe.ToBytes(pid);
                    }
                    ms = new MemoryStream();
                    formatter.Serialize(ms, kv);
                    yield return ms.ToArray();
                }
            }
        }

        [Serializable]
        private class MapValuesHelper<K, V, U>
        {
            private readonly Func<V, U> func;
            public MapValuesHelper(Func<V, U> f)
            {
                func = f;
            }

            public Tuple<K, U> Execute(Tuple<K, V> kvp)
            {
                return new Tuple<K, U>
                    (
                    kvp.Item1,
                    func(kvp.Item2)
                    );
            }
        }

        [Serializable]
        private class FlatMapValuesHelper<K, V, U>
        {
            private readonly Func<V, IEnumerable<U>> func;
            public FlatMapValuesHelper(Func<V, IEnumerable<U>> f)
            {
                func = f;
            }

            public IEnumerable<Tuple<K, U>> Execute(Tuple<K, V> kvp)
            {
                return func(kvp.Item2).Select(v => new Tuple<K, U>(kvp.Item1, v));
            }
        }
        [Serializable]
        public class LookupHelper<K, V>
        {
            private readonly K key;
            public LookupHelper(K key)
            {
                this.key = key;
            }
            public bool Execute(Tuple<K, V> input)
            {
                return input.Item1.ToString() == key.ToString();
            }
        }

        [Serializable]
        public class PartitionFuncDynamicTypeHelper<K>
        {
            private readonly Func<K, int> func;
            public PartitionFuncDynamicTypeHelper(Func<K, int> f)
            {
                this.func = f;
            }
            public int Execute(dynamic input)
            {
                return func((K)input);
            }
        }

        /// <summary>
        /// Converts a collection to a list where the element type is Option(T) type.
        /// If the collection is empty, just returns the empty list.
        /// </summary>
        /// <param name="list">The collection that be inputted to convert</param>
        /// <typeparam name="T">The element type in the collection</typeparam>
        /// <returns>A list that use Option(T) as element type</returns>
        public static List<Option<T>> NullIfEmpty<T>(this IEnumerable<T> list)
        {
            return list.Any() ? list.Select(v => new Option<T>(v)).ToList() : new List<Option<T>>() { new Option<T>() };
        }

        private static long GenerateObjectId(object obj)
        {
            if (obj == null)
                return 0;

            MD5 md5 = MD5.Create();
            IFormatter formatter = new BinaryFormatter();
            var ms = new MemoryStream();
            formatter.Serialize(ms, obj);
            var hash = md5.ComputeHash(ms.ToArray());
            return BitConverter.ToInt64(hash.Take(8).ToArray(), 0);
        }
    }
}
