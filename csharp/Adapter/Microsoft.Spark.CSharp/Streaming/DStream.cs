﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

using Microsoft.Spark.CSharp.Proxy;
using Microsoft.Spark.CSharp.Core;
using Microsoft.Spark.CSharp.Interop;

namespace Microsoft.Spark.CSharp.Streaming
{
    /// <summary>
    /// A Discretized Stream (DStream), the basic abstraction in Spark Streaming,
    /// is a continuous sequence of RDDs (of the same type) representing a
    /// continuous stream of data (see <see cref="RDD{T}"/>) in the Spark core documentation
    /// for more details on RDDs).
    /// 
    /// DStreams can either be created from live data (such as, data from TCP
    /// sockets, Kafka, Flume, etc.) using a <see cref="StreamingContext"/> or it can be
    /// generated by transforming existing DStreams using operations such as
    /// `Map`, `Window` and `ReduceByKeyAndWindow`. While a Spark Streaming
    /// program is running, each DStream periodically generates a RDD, either
    /// from live data or by transforming the RDD generated by a parent DStream.
    /// 
    /// DStreams internally is characterized by a few basic properties:
    ///  - A list of other DStreams that the DStream depends on
    ///  - A time interval at which the DStream generates an RDD
    ///  - A function that is used to generate an RDD after each time interval
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class DStream<T>
    {
        private static DateTime startUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public StreamingContext streamingContext;
        public IDStreamProxy prevDStreamProxy;
        public IDStreamProxy dstreamProxy;
        public SerializedMode prevSerializedMode;
        public SerializedMode serializedMode;
        
        public bool isCached;
        public bool isCheckpointed;

        public virtual IDStreamProxy DStreamProxy { get { return dstreamProxy; } }
        
        public bool Piplinable
        {
            get
            {
                return this is TransformedDStream<T> && !isCached && !isCheckpointed;
            }
        }

        /// <summary>
        /// Return the slideDuration in seconds of this DStream
        /// </summary>
        public int SlideDuration
        {
            get
            {
                return DStreamProxy.SlideDuration;
            }
        }

        public DStream() { }

        public DStream(IDStreamProxy dstreamProxy, StreamingContext streamingContext, SerializedMode serializedMode = SerializedMode.Byte)
        {
            this.streamingContext = streamingContext;
            this.dstreamProxy = dstreamProxy;
            this.serializedMode = serializedMode;
        }

        /// <summary>
        /// Return a new DStream in which each RDD has a single element
        /// generated by counting each RDD of this DStream.
        /// </summary>
        /// <returns></returns>
        public DStream<long> Count()
        {
            return MapPartitionsWithIndex((p, x) => new long[] { x.LongCount() }).Reduce((x, y) => x + y);
        }

        /// <summary>
        /// Return a new DStream containing only the elements that satisfy predicate.
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public DStream<T> Filter(Func<T, bool> f)
        {
            return MapPartitionsWithIndex((new FilterHelper<T>(f)).Execute, true);
        }

        /// <summary>
        /// Return a new DStream by applying a function to all elements of
        /// this DStream, and then flattening the results
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="f"></param>
        /// <param name="preservesPartitioning"></param>
        /// <returns></returns>
        public DStream<U> FlatMap<U>(Func<T, IEnumerable<U>> f, bool preservesPartitioning = false)
        {
            return MapPartitionsWithIndex(new FlatMapHelper<T, U>(f).Execute, preservesPartitioning);
        }

        /// <summary>
        /// Return a new DStream by applying a function to each element of DStream.
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="f"></param>
        /// <param name="preservesPartitioning"></param>
        /// <returns></returns>
        public DStream<U> Map<U>(Func<T, U> f, bool preservesPartitioning = false)
        {
            return MapPartitionsWithIndex(new MapHelper<T, U>(f).Execute, preservesPartitioning);
        }

        /// <summary>
        /// Return a new DStream in which each RDD is generated by applying
        /// mapPartitions() to each RDDs of this DStream.
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="f"></param>
        /// <param name="preservesPartitioning"></param>
        /// <returns></returns>
        public DStream<U> MapPartitions<U>(Func<IEnumerable<T>, IEnumerable<U>> f, bool preservesPartitioning = false)
        {
            return MapPartitionsWithIndex(new MapPartitionsHelper<T, U>(f).Execute, preservesPartitioning);
        }

        /// <summary>
        /// Return a new DStream in which each RDD is generated by applying
        /// mapPartitionsWithIndex() to each RDDs of this DStream.
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <returns></returns>
        public DStream<U> MapPartitionsWithIndex<U>(Func<int, IEnumerable<T>, IEnumerable<U>> f, bool preservesPartitioningParam = false)
        {
            return Transform<U>(new MapPartitionsWithIndexHelper<T, U>(f, preservesPartitioningParam).Execute);
        }

        /// <summary>
        /// Return a new DStream in which each RDD has a single element
        /// generated by reducing each RDD of this DStream.
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public DStream<T> Reduce(Func<T, T, T> f)
        {
            return Map<Tuple<string, T>>(x => new Tuple<string, T>(string.Empty, x)).ReduceByKey(f, 1).Map<T>(kvp => kvp.Item2);
        }

        /// <summary>
        /// Apply a function to each RDD in this DStream.
        /// </summary>
        /// <param name="f"></param>
        public void ForeachRDD(Action<RDD<T>> f)
        {
            ForeachRDD(new ForeachRDDHelper<T>(f).Execute);
        }

        /// <summary>
        /// Apply a function to each RDD in this DStream.
        /// </summary>
        /// <param name="f"></param>
        public void ForeachRDD(Action<double, RDD<dynamic>> f)
        {
            var formatter = new BinaryFormatter();
            var stream = new MemoryStream();
            formatter.Serialize(stream, f);
            DStreamProxy.CallForeachRDD(stream.ToArray(), serializedMode.ToString());
        }

        /// <summary>
        /// Print the first num elements of each RDD generated in this DStream.
        ///
        /// @param num: the number of elements from the first will be printed.
        /// </summary>
        /// <param name="num"></param>
        public void Print(int num = 10)
        {
            DStreamProxy.Print(num);
        }

        /// <summary>
        /// Return a new DStream in which RDD is generated by applying glom() to RDD of this DStream.
        /// </summary>
        /// <returns></returns>
        public DStream<T[]> Glom()
        {
            return MapPartitionsWithIndex((pid, iter) => new List<T[]> { iter.ToArray() });
        }

        /// <summary>
        /// Persist the RDDs of this DStream with the default storage level <see cref="StorageLevelType.MEMORY_ONLY_SER"/>.
        /// </summary>
        /// <returns></returns>
        public DStream<T> Cache()
        {
            return Persist(StorageLevelType.MEMORY_ONLY_SER);
        }

        /// <summary>
        /// Persist the RDDs of this DStream with the given storage level
        /// </summary>
        /// <param name="storageLevelType"></param>
        /// <returns></returns>
        public DStream<T> Persist(StorageLevelType storageLevelType)
        {
            isCached = true;
            DStreamProxy.Persist(storageLevelType);
            return this;
        }

        /// <summary>
        /// Enable periodic checkpointing of RDDs of this DStream
        /// </summary>
        /// <param name="intervalMs">time in milliseconds, after each period of that, generated RDD will be checkpointed</param>
        /// <returns></returns>
        public DStream<T> Checkpoint(long intervalMs)
        {
            isCheckpointed = true;
            DStreamProxy.Checkpoint(intervalMs);
            return this;
        }

        /// <summary>
        /// Return a new DStream in which each RDD contains the counts of each
        /// distinct value in each RDD of this DStream.
        /// </summary>
        /// <returns></returns>
        public DStream<Tuple<T, long>> CountByValue(int numPartitions = 0)
        {
            return Map(v => new Tuple<T, long>(v, 1L)).ReduceByKey((x, y) => x + y, numPartitions);
        }

        /// <summary>
        /// Save each RDD in this DStream as text file, using string representation of elements.
        /// </summary>
        public void SaveAsTextFiles(string prefix, string suffix = null)
        {
            ForeachRDD(new SaveAsTextFileHelper(prefix, suffix).Execute);
        }

        /// <summary>
        /// Return a new DStream in which each RDD is generated by applying a function
        /// on each RDD of this DStream.
        /// 
        /// `func` can have one argument of `rdd`, or have two arguments of
        /// (`time`, `rdd`)
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="f"></param>
        /// <returns></returns>
        public DStream<U> Transform<U>(Func<RDD<T>, RDD<U>> f)
        {
            return Transform<U>(new TransformHelper<T, U>(f).Execute);
        }

        /// <summary>
        /// Return a new DStream in which each RDD is generated by applying a function
        /// on each RDD of this DStream.
        /// 
        /// `func` can have one argument of `rdd`, or have two arguments of
        /// (`time`, `rdd`)
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <param name="f"></param>
        /// <returns></returns>
        public DStream<U> Transform<U>(Func<double, RDD<T>, RDD<U>> f)
        {
            TransformedDStream<U> transformedDStream = new TransformedDStream<U>();
            transformedDStream.Init<T>(this, new TransformDynamicHelper<T, U>(f).Execute);
            return transformedDStream;
        }

        ///  <summary>
        ///  Return a new DStream in which each RDD is generated by applying a function
        ///  on each RDD of this DStream and 'other' DStream.
        /// 
        ///  `func` can have two arguments of (`rdd_a`, `rdd_b`) or have three
        ///  arguments of (`time`, `rdd_a`, `rdd_b`)
        ///  </summary>
        ///  <typeparam name="U"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="f"></param>
        ///  <param name="other"></param>
        ///  <param name="keepSerializer"></param>
        ///  <returns></returns>
        public DStream<V> TransformWith<U, V>(Func<RDD<T>, RDD<U>, RDD<V>> f, DStream<U> other, bool keepSerializer = false)
        {
            return TransformWith<U, V>(new TransformWithHelper<T, U, V>(f).Execute, other, keepSerializer);
        }

        /// <summary>
        /// Return a new DStream in which each RDD is generated by applying a function
        /// on each RDD of this DStream and 'other' DStream.
        ///
        /// `func` can have two arguments of (`rdd_a`, `rdd_b`) or have three
        /// arguments of (`time`, `rdd_a`, `rdd_b`)
        /// </summary>
        /// <typeparam name="U"></typeparam>
        /// <typeparam name="V"></typeparam>
        /// <param name="f"></param>
        /// <param name="other"></param>
        /// <param name="keepSerializer"></param>
        /// <returns></returns>
        public DStream<V> TransformWith<U, V>(Func<double, RDD<T>, RDD<U>, RDD<V>> f, DStream<U> other, bool keepSerializer = false)
        {
            Func<double, RDD<dynamic>, RDD<dynamic>> prevF = Piplinable ? (this as TransformedDStream<T>).func : null;
            Func<double, RDD<dynamic>, RDD<dynamic>> otherF = other.Piplinable ? (other as TransformedDStream<U>).func : null;

            Func<double, RDD<dynamic>, RDD<dynamic>, RDD<dynamic>> func = new TransformWithDynamicHelper<T, U, V>(f, prevF, otherF).Execute;
            
            var formatter = new BinaryFormatter();
            var stream = new MemoryStream();
            formatter.Serialize(stream, func);

            return new DStream<V>(SparkCLREnvironment.SparkCLRProxy.StreamingContextProxy.CreateCSharpTransformed2DStream(
                    Piplinable ? prevDStreamProxy : DStreamProxy, 
                    other.Piplinable ? other.prevDStreamProxy : other.DStreamProxy, 
                    stream.ToArray(),
                    (Piplinable ? prevSerializedMode : serializedMode).ToString(), 
                    (other.Piplinable ? other.prevSerializedMode : other.serializedMode).ToString()), 
                streamingContext,
                keepSerializer ? serializedMode : SerializedMode.Byte);
        }

        /// <summary>
        /// Return a new DStream with an increased or decreased level of parallelism.
        /// </summary>
        /// <param name="numPartitions"></param>
        /// <returns></returns>
        public DStream<T> Repartition(int numPartitions)
        {
            return Transform<T>(new RepartitionHelper<T>(numPartitions).Execute);
        }

        /// <summary>
        /// Return a new DStream by unifying data of another DStream with this DStream.
        ///
        /// @param other: Another DStream having the same interval (i.e., slideDuration) as this DStream.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public DStream<T> Union(DStream<T> other)
        {
            if (SlideDuration != other.SlideDuration)
            {
                throw new ArgumentException("the two DStream should have same slide duration");
            }

            return TransformWith((rdd1, rdd2) => rdd1.Union(rdd2), other, true);
        }

        /// <summary>
        /// Return all the RDDs between 'fromTime' to 'toTime' (both included)
        /// </summary>
        /// <param name="fromTimeUtc"></param>
        /// <param name="toTimeUtc"></param>
        /// <returns></returns>
        public RDD<T>[] Slice(DateTime fromTimeUtc, DateTime toTimeUtc)
        {
            long fromUnixTime = (long)(fromTimeUtc - startUtc).TotalMilliseconds;
            long toUnixTime = (long)(toTimeUtc - startUtc).TotalMilliseconds;

            return DStreamProxy.Slice(fromUnixTime, toUnixTime).Select(r => new RDD<T>(r, streamingContext.SparkContext, serializedMode)).ToArray();
        }

        public void ValidateWindowParam(int windowSeconds, int slideSeconds)
        {
            int duration = SlideDuration;

            if ((windowSeconds * 1000) % duration != 0)
            {
                throw new ArgumentException(string.Format("windowDuration must be multiple of the slide duration ({0} ms)", duration));
            }

            if (slideSeconds > 0 && (slideSeconds * 1000) % duration != 0)
            {
                throw new ArgumentException(string.Format("slideDuration must be multiple of the slide duration ({0} ms)", duration));
            }
        }

        /// <summary>
        /// Return a new DStream in which each RDD contains all the elements in seen in a
        /// sliding window of time over this DStream.
        ///
        /// @param windowDuration: width of the window; must be a multiple of this DStream's
        ///                      batching interval
        /// @param slideDuration:  sliding interval of the window (i.e., the interval after which
        ///                      the new DStream will generate RDDs); must be a multiple of this
        ///                      DStream's batching interval
        /// </summary>
        /// <param name="windowSeconds"></param>
        /// <param name="slideSeconds"></param>
        /// <returns></returns>
        public DStream<T> Window(int windowSeconds, int slideSeconds)
        {
            ValidateWindowParam(windowSeconds, slideSeconds);
            return new DStream<T>(DStreamProxy.Window(windowSeconds, slideSeconds), streamingContext, serializedMode);
        }

        /// <summary>
        /// Return a new DStream in which each RDD has a single element generated by reducing all
        /// elements in a sliding window over this DStream.
        ///
        /// if `invReduceFunc` is not None, the reduction is done incrementally
        /// using the old window's reduced value :
        ///
        /// 1. reduce the new values that entered the window (e.g., adding new counts)
        ///
        /// 2. "inverse reduce" the old values that left the window (e.g., subtracting old counts)
        /// This is more efficient than `invReduceFunc` is None.
        ///
        /// </summary>
        /// <param name="reduceFunc">associative reduce function</param>
        /// <param name="invReduceFunc">inverse reduce function of `reduceFunc`</param>
        /// <param name="windowSeconds">width of the window; must be a multiple of this DStream's batching interval</param>
        /// <param name="slideSeconds">sliding interval of the window (i.e., the interval after which the new DStream will generate RDDs); must be a multiple of this DStream's batching interval</param>
        /// <returns></returns>
        public DStream<T> ReduceByWindow(Func<T, T, T> reduceFunc, Func<T, T, T> invReduceFunc, int windowSeconds, int slideSeconds = 0)
        {
            var keyed = Map(v => new Tuple<int, T>(1, v));
            var reduced = keyed.ReduceByKeyAndWindow(reduceFunc, invReduceFunc, windowSeconds, slideSeconds, 1);
            return reduced.Map(kv => (T)kv.Item2);
        }

        /// <summary>
        /// Return a new DStream in which each RDD has a single element generated
        /// by counting the number of elements in a window over this DStream.
        /// windowDuration and slideDuration are as defined in the window() operation.
        /// 
        /// This is equivalent to window(windowDuration, slideDuration).count(),
        /// but will be more efficient if window is large.
        /// </summary>
        /// <param name="windowSeconds"></param>
        /// <param name="slideSeconds"></param>
        /// <returns></returns>
        public DStream<long> CountByWindow(int windowSeconds, int slideSeconds = 0)
        {
            return Map(x => 1L).ReduceByWindow((x, y) => x + y, (x, y) => x - y, windowSeconds, slideSeconds);
        }

        /// <summary>
        /// Return a new DStream in which each RDD contains the count of distinct elements in
        /// RDDs in a sliding window over this DStream.
        /// </summary>
        /// <param name="windowSeconds">width of the window; must be a multiple of this DStream's batching interval</param>
        /// <param name="slideSeconds">
        ///     sliding interval of the window (i.e., the interval after which
        ///     the new DStream will generate RDDs); must be a multiple of this
        ///     DStream's batching interval        
        /// </param>
        /// <param name="numPartitions">number of partitions of each RDD in the new DStream.</param>
        /// <returns></returns>
        public DStream<long> CountByValueAndWindow(int windowSeconds, int slideSeconds, int numPartitions = 0)
        {
            var keyed = Map(v => new Tuple<T, int>(v, 1));
            var counted = keyed.ReduceByKeyAndWindow((x, y) => x + y, (x, y) => x - y, windowSeconds, slideSeconds, numPartitions);
            return counted.Filter(kv => kv.Item2 > 0).Count();
        }
    }

    /// <summary>
    /// Following classes are defined explicitly instead of using anonymous method as delegate to prevent C# compiler from generating
    /// private anonymous type that is not marked serializable. Since the delegate has to be serialized and sent to the Spark workers
    /// for execution, it is necessary to have the type marked [Serializable]. These classes are to work around the limitation
    /// on the serializability of compiler generated types
    /// </summary>

    [Serializable]
    public class MapPartitionsWithIndexHelper<I, O>
    {
        private readonly Func<int, IEnumerable<I>, IEnumerable<O>> func;
        private readonly bool preservesPartitioningParam = false;
        public MapPartitionsWithIndexHelper(Func<int, IEnumerable<I>, IEnumerable<O>> f, bool preservesPartitioningParam = false)
        {
            func = f;
            this.preservesPartitioningParam = preservesPartitioningParam;
        }

        public RDD<O> Execute(RDD<I> rdd)
        {
            return rdd.MapPartitionsWithIndex(func, preservesPartitioningParam);
        }
    }

    [Serializable]
    public class TransformHelper<I, O>
    {
        private readonly Func<RDD<I>, RDD<O>> func;
        public TransformHelper(Func<RDD<I>, RDD<O>> f)
        {
            func = f;
        }

        public RDD<O> Execute(double t, RDD<I> rdd)
        {
            return func(rdd);
        }
    }

    [Serializable]
    public class TransformDynamicHelper<I, O>
    {
        private readonly Func<double, RDD<I>, RDD<O>> func;
        public TransformDynamicHelper(Func<double, RDD<I>, RDD<O>> f)
        {
            func = f;
        }

        public RDD<dynamic> Execute(double t, RDD<dynamic> rdd)
        {
            return func(t, rdd.ConvertTo<I>()).ConvertTo<dynamic>();
        }
    }

    [Serializable]
    public class TransformWithHelper<T, U, V>
    {
        private readonly Func<RDD<T>, RDD<U>, RDD<V>> func;
        public TransformWithHelper(Func<RDD<T>, RDD<U>, RDD<V>> f)
        {
            func = f;
        }

        public RDD<V> Execute(double t, RDD<T> rdd1, RDD<U> rdd2)
        {
            return func(rdd1, rdd2);
        }
    }

    [Serializable]
    public class TransformWithDynamicHelper<T, U, V>
    {
        private readonly Func<double, RDD<T>, RDD<U>, RDD<V>> func;
        private readonly Func<double, RDD<dynamic>, RDD<dynamic>> prevFunc;
        private readonly Func<double, RDD<dynamic>, RDD<dynamic>> otherFunc;

        public TransformWithDynamicHelper(Func<double, RDD<T>, RDD<U>, RDD<V>> f, Func<double, RDD<dynamic>, RDD<dynamic>> prevF, Func<double, RDD<dynamic>, RDD<dynamic>> otherF)
        {
            func = f;
            prevFunc = prevF;
            otherFunc = otherF;
        }

        public RDD<dynamic> Execute(double t, RDD<dynamic> rdd1, RDD<dynamic> rdd2)
        {
            if (prevFunc != null)
                rdd1 = prevFunc(t, rdd1);

            if (otherFunc != null)
                rdd2 = otherFunc(t, rdd2);
            
            return func(t, rdd1.ConvertTo<T>(), rdd2.ConvertTo<U>()).ConvertTo<dynamic>();
        }
    }

    [Serializable]
    public class RepartitionHelper<T>
    {
        private readonly int numPartitions;
        public RepartitionHelper(int numPartitions)
        {
            this.numPartitions = numPartitions;
        }

        public RDD<T> Execute(RDD<T> rdd)
        {
            return rdd.Repartition(numPartitions);
        }
    }

    [Serializable]
    public class ForeachRDDHelper<I>
    {
        private readonly Action<RDD<I>> func;
        public ForeachRDDHelper(Action<RDD<I>> f)
        {
            func = f;
        }

        public void Execute(double t, RDD<dynamic> rdd)
        {
            func(rdd.ConvertTo<I>());
        }
    }

    [Serializable]
    public class SaveAsTextFileHelper
    {
        private readonly string prefix; 
        private readonly string suffix;
        
        public SaveAsTextFileHelper(string prefix, string suffix)
        {
            this.prefix = prefix;
            this.suffix = suffix;
        }

        public void Execute(double t, RDD<dynamic> rdd)
        {
            rdd.ConvertTo<string>().SaveAsTextFile(prefix + (long)t + suffix);
        }
    }
}
