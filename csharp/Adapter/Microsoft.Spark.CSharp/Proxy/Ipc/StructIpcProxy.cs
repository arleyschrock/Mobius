﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Spark.CSharp.Interop;
using Microsoft.Spark.CSharp.Interop.Ipc;

namespace Microsoft.Spark.CSharp.Proxy.Ipc
{
    [ExcludeFromCodeCoverage] //IPC calls to JVM validated using validation-enabled samples - unit test coverage not reqiured
    public class StructTypeIpcProxy : IStructTypeProxy
    {
        private readonly JvmObjectReference jvmStructTypeReference;

        public JvmObjectReference JvmStructTypeReference
        {
            get { return jvmStructTypeReference; }
        }

        public StructTypeIpcProxy(JvmObjectReference jvmStructTypeReference)
        {
            this.jvmStructTypeReference = jvmStructTypeReference;
        }

        public List<IStructFieldProxy> GetStructTypeFields()
        {
            var fieldsReferenceList = SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(jvmStructTypeReference, "fields");
            return (fieldsReferenceList as List<JvmObjectReference>).Select(s => new StructFieldIpcProxy(s)).Cast<IStructFieldProxy>().ToList();
        }

        public string ToJson()
        {
            return SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(jvmStructTypeReference, "json").ToString();
        }
    }

    public class StructDataTypeIpcProxy : IStructDataTypeProxy
    {
        public readonly JvmObjectReference jvmStructDataTypeReference;

        public StructDataTypeIpcProxy(JvmObjectReference jvmStructDataTypeReference)
        {
            this.jvmStructDataTypeReference = jvmStructDataTypeReference;
        }

        public string GetDataTypeString()
        {
            return SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(jvmStructDataTypeReference, "toString").ToString();
        }

        public string GetDataTypeSimpleString()
        {
            return SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(jvmStructDataTypeReference, "simpleString").ToString();
        }
    }

    public class StructFieldIpcProxy : IStructFieldProxy
    {
        private readonly JvmObjectReference jvmStructFieldReference;
        public JvmObjectReference JvmStructFieldReference { get { return jvmStructFieldReference; } }

        public StructFieldIpcProxy(JvmObjectReference jvmStructFieldReference)
        {
            this.jvmStructFieldReference = jvmStructFieldReference;
        }

        public string GetStructFieldName()
        {
            return SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(jvmStructFieldReference, "name").ToString();
        }

        public IStructDataTypeProxy GetStructFieldDataType()
        {
            return new StructDataTypeIpcProxy(new JvmObjectReference(SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(jvmStructFieldReference, "dataType").ToString()));
        }

        public bool GetStructFieldIsNullable()
        {
            return bool.Parse(SparkCLRIpcProxy.JvmBridge.CallNonStaticJavaMethod(jvmStructFieldReference, "nullable").ToString());
        }
    }
}
