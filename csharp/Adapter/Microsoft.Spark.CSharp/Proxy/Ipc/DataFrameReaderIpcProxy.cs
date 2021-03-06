﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Spark.CSharp.Interop.Ipc;
using Microsoft.Spark.CSharp.Sql;

namespace Microsoft.Spark.CSharp.Proxy.Ipc
{
    [ExcludeFromCodeCoverage] //IPC calls to JVM validated using validation-enabled samples - unit test coverage not reqiured
    public class DataFrameReaderIpcProxy : IDataFrameReaderProxy
    {
        private readonly JvmObjectReference jvmDataFrameReaderReference;
        private readonly ISqlContextProxy sqlContextProxy;

        public DataFrameReaderIpcProxy(JvmObjectReference jvmDataFrameReaderReference, ISqlContextProxy sqlContextProxy)
        {
            this.jvmDataFrameReaderReference = jvmDataFrameReaderReference;
            this.sqlContextProxy = sqlContextProxy;
        }

        public void Format(string source)
        {
            SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(jvmDataFrameReaderReference, "format", new object[] { source });
        }

        public void Schema(StructType schema)
        {
            var structTypeIpcProxy = schema.StructTypeProxy as StructTypeIpcProxy;
            if (structTypeIpcProxy != null)
                SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(jvmDataFrameReaderReference, "schema", 
                    new object[] { structTypeIpcProxy.JvmStructTypeReference });
        }

        public void Options(Dictionary<string, string> options)
        {
            SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(jvmDataFrameReaderReference, "options", new object[] { options });
        }

        public IDataFrameProxy Load()
        {
            return new DataFrameIpcProxy(new JvmObjectReference(
                SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(jvmDataFrameReaderReference, "load").ToString()), sqlContextProxy);
        }

        public IDataFrameProxy Jdbc(string url, string table, string[] predicates, Dictionary<string, string> connectionProperties)
        {
            var propertiesJvmReference = SparkCLRIpcProxy.JvmBridge.CallConstructor("java.util.Properties", new object[] { });
            if (connectionProperties != null)
            {
                foreach (var property in connectionProperties)
                {
                    SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(propertiesJvmReference, "setProperty",
                        new object[] { property.Key, property.Value });
                }
            }

            return new DataFrameIpcProxy(new JvmObjectReference(
                SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(
                jvmDataFrameReaderReference, "jdbc", new object[] { url, table, predicates, propertiesJvmReference }).ToString()), sqlContextProxy);
        }

        public IDataFrameProxy Jdbc(string url, string table, Dictionary<string, string> properties)
        {
            var propertiesJvmReference = SparkCLRIpcProxy.JvmBridge.CallConstructor("java.util.Properties", new object[] {});
            if (properties != null)
            {
                foreach (var property in properties)
                {
                    SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(propertiesJvmReference, "setProperty",
                        new object[] {property.Key, property.Value});
                }
            }

            return new DataFrameIpcProxy(new JvmObjectReference(
                SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(
                jvmDataFrameReaderReference, "jdbc", new object[] { url, table, propertiesJvmReference }).ToString()), sqlContextProxy);
        }

        public IDataFrameProxy Jdbc(string url, string table, string columnName, string lowerBound, string upperBound, int numPartitions, Dictionary<string, string> connectionProperties)
        {
            var propertiesJvmReference = SparkCLRIpcProxy.JvmBridge.CallConstructor("java.util.Properties", new object[] { });
            if (connectionProperties != null)
            {
                foreach (var property in connectionProperties)
                {
                    SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(propertiesJvmReference, "setProperty",
                        new object[] { property.Key, property.Value });
                }
            }

            return new DataFrameIpcProxy(new JvmObjectReference(
                SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(
                jvmDataFrameReaderReference, "jdbc", new object[] { url, table, columnName, lowerBound, upperBound, numPartitions, propertiesJvmReference }).ToString()),
                sqlContextProxy);
        }

        public IDataFrameProxy Parquet(string[] paths)
        {
            return new DataFrameIpcProxy(new JvmObjectReference(
                SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(
                jvmDataFrameReaderReference, "parquet", new object[] { paths }).ToString()), sqlContextProxy);
        }

        public IDataFrameProxy Table(string tableName)
        {
            return new DataFrameIpcProxy(new JvmObjectReference(
                SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(
                jvmDataFrameReaderReference, "table", new object[] { tableName }).ToString()), sqlContextProxy);
        }
    }
}
