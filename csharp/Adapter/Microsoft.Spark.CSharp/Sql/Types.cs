﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Spark.CSharp.Interop.Ipc;
using Microsoft.Spark.CSharp.Proxy;
using Microsoft.Spark.CSharp.Proxy.Ipc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Spark.CSharp.Sql
{
    /// <summary>
    /// The base type of all Spark SQL data types.
    /// </summary>
    [Serializable]
    public abstract class DataType
    {
        /// <summary>
        /// Trim "Type" in the end from class name, ToLower() to align with Scala.
        /// </summary>
        public string TypeName
        {
            get { return NormalizeTypeName(GetType().Name); }
        }

        /// <summary>
        /// return TypeName by default, subclass can override it
        /// </summary>
        public virtual string SimpleString
        {
            get { return TypeName; }
        }

        /// <summary>
        /// return only type: TypeName by default, subclass can override it
        /// </summary>
        public virtual object JsonValue { get { return TypeName; } }

        /// <summary>
        /// The compact JSON representation of this data type.
        /// </summary>
        public string Json
        {
            get
            {
                var jObject = JsonValue is JObject ? ((JObject)JsonValue).SortProperties() : JsonValue;
                return JsonConvert.SerializeObject(jObject, Formatting.None);
            }
        }

        /// <summary>
        /// Parses a Json string to construct a DataType.
        /// </summary>
        /// <param name="json">The Json string to be parsed</param>
        /// <returns>The new DataType instance from the Json string</returns>
        public static DataType ParseDataTypeFromJson(string json)
        {
            return ParseDataTypeFromJson(JToken.Parse(json));
        }

        /// <summary>
        /// Parse a JToken object to construct a DataType.
        /// </summary>
        /// <param name="json">The JToken object to be parsed</param>
        /// <returns>The new DataType instance from the Json string</returns>
        /// <exception cref="NotImplementedException">Not implemented for "udt" type</exception>
        /// <exception cref="ArgumentException"></exception>
        protected static DataType ParseDataTypeFromJson(JToken json)
        {
            if (json.Type == JTokenType.Object) // {name: address, type: {type: struct,...},...}
            {
                JToken type;
                var typeJObject = (JObject)json;
                if (typeJObject.TryGetValue("type", out type))
                {
                    Type complexType;
                    if ((complexType = ComplexTypes.FirstOrDefault(ct => NormalizeTypeName(ct.Name) == type.ToString())) != default(Type))
                    {
                        return ((ComplexType)Activator.CreateInstance(complexType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
                            , null, new object[] { typeJObject }, null)); // create new instance of ComplexType
                    }
                    if (type.ToString() == "udt")
                    {
                        // TODO
                        throw new NotImplementedException();
                    }
                }
                throw new ArgumentException(string.Format("Could not parse data type: {0}", type));
            }
            else // {name: age, type: bigint,...} // TODO: validate more JTokenType other than Object
            {
                return ParseAtomicType(json);
            }

        }

        private static AtomicType ParseAtomicType(JToken type)
        {
            Type atomicType;
            if ((atomicType = AtomicTypes.FirstOrDefault(at => NormalizeTypeName(at.Name) == type.ToString())) != default(Type))
            {
                return (AtomicType)Activator.CreateInstance(atomicType); // create new instance of AtomicType
            }

            Match fixedDecimal = DecimalType.FixedDecimal.Match(type.ToString());
            if (fixedDecimal.Success)
            {
                return new DecimalType(int.Parse(fixedDecimal.Groups[1].Value), int.Parse(fixedDecimal.Groups[2].Value));
            }

            throw new ArgumentException(string.Format("Could not parse data type: {0}", type));
        }

        [NonSerialized]
        private static readonly Type[] AtomicTypes = typeof(AtomicType).Assembly.GetTypes().Where(type =>
            type.IsSubclassOf(typeof(AtomicType))).ToArray();

        [NonSerialized]
        private static readonly Type[] ComplexTypes = typeof(ComplexType).Assembly.GetTypes().Where(type =>
            type.IsSubclassOf(typeof(ComplexType))).ToArray();

        [NonSerialized]
        private static readonly Func<string, string> NormalizeTypeName = s => s.Substring(0, s.Length - 4).ToLower(); // trim "Type" at the end of type name


    }

    /// <summary>
    /// An public type used to represent a simple type. 
    /// </summary>
    [Serializable]
    public class AtomicType : DataType
    {
    }

    /// <summary>
    /// An public type used to represent a complex type (such as arrays, structs, and maps).
    /// </summary>
    [Serializable]
    public abstract class ComplexType : DataType
    {
        /// <summary>
        /// Abstract method that constructs a complex type from a Json object
        /// </summary>
        /// <param name="json">The Json object to construct a complex type</param>
        /// <returns>A new constructed complex type</returns>
        public abstract DataType FromJson(JObject json);
        /// <summary>
        /// Constructs a complex type from a Json string
        /// </summary>
        /// <param name="json">The string that represents a Json.</param>
        /// <returns>A new constructed complex type</returns>
        public DataType FromJson(string json)
        {
            return FromJson(JObject.Parse(json));
        }
    }

    /// <summary>
    /// The data type representing NULL values.
    /// </summary>
    [Serializable]
    public class NullType : AtomicType { }

    /// <summary>
    /// The data type representing String values.
    /// </summary>
    [Serializable]
    public class StringType : AtomicType { }

    /// <summary>
    /// The data type representing binary values.
    /// </summary>
    [Serializable]
    public class BinaryType : AtomicType { }

    /// <summary>
    /// The data type representing Boolean values.
    /// </summary>
    [Serializable]
    public class BooleanType : AtomicType { }

    /// <summary>
    /// The data type representing Date values.
    /// </summary>
    [Serializable]
    public class DateType : AtomicType { }

    /// <summary>
    /// The data type representing Timestamp values. 
    /// </summary>
    [Serializable]
    public class TimestampType : AtomicType { }

    /// <summary>
    /// The data type representing Double values.
    /// </summary>
    [Serializable]
    public class DoubleType : AtomicType { }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class FloatType : AtomicType { }

    /// <summary>
    /// The data type representing Float values.
    /// </summary>
    [Serializable]
    public class ByteType : AtomicType { }

    /// <summary>
    /// 
    /// </summary>
    [Serializable]
    public class IntegerType : AtomicType { }

    /// <summary>
    /// The data type representing Int values.
    /// </summary>
    [Serializable]
    public class LongType : AtomicType { }

    /// <summary>
    /// The data type representing Short values.
    /// </summary>
    [Serializable]
    public class ShortType : AtomicType { }

    /// <summary>
    /// The data type representing Decimal values.
    /// </summary>
    [Serializable]
    public class DecimalType : AtomicType
    {
        /// <summary>
        /// Gets the regular expression that represents a fixed decimal. 
        /// </summary>
        public static Regex FixedDecimal = new Regex(@"decimal\((\d+),\s(\d+)\)");
        private int? precision, scale;
        /// <summary>
        /// Initializes a new instance of DecimalType from parameters specifying its precision and scale.
        /// </summary>
        /// <param name="precision">The precision of the type</param>
        /// <param name="scale">The scale of the type</param>
        public DecimalType(int? precision = null, int? scale = null)
        {
            this.precision = precision;
            this.scale = scale;
        }

        public override object JsonValue
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Constructs a DecimalType from a Json object
        /// </summary>
        /// <param name="json">The Json object used to construct a DecimalType</param>
        /// <returns>A new DecimalType instance</returns>
        /// <exception cref="NotImplementedException">Not implemented yet.</exception>
        public DataType FromJson(JObject json)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// The data type for collections of multiple values. 
    /// </summary>
    [Serializable]
    public class ArrayType : ComplexType
    {
        /// <summary>
        /// Gets the DataType of each element in the array
        /// </summary>
        public DataType ElementType { get { return elementType; } }
        /// <summary>
        /// Returns whether the array can contain null (None) values
        /// </summary>
        public bool ContainsNull { get { return containsNull; } }

        /// <summary>
        /// Initializes a ArrayType instance with a specific DataType and specifying if the array has null values.
        /// </summary>
        /// <param name="elementType">The data type of values</param>
        /// <param name="containsNull">Indicates if values have null values</param>
        public ArrayType(DataType elementType, bool containsNull = true)
        {
            this.elementType = elementType;
            this.containsNull = containsNull;
        }

        public ArrayType(JObject json)
        {
            FromJson(json);
        }

        /// <summary>
        /// Readable string representation for the type.
        /// </summary>
        public override string SimpleString
        {
            get { return string.Format("array<{0}>", elementType.SimpleString); }
        }

        public override object JsonValue
        {
            get
            {
                return new JObject(
                                  new JProperty("type", TypeName),
                                  new JProperty("elementType", elementType.JsonValue),
                                  new JProperty("containsNull", containsNull));
            }
        }

        /// <summary>
        /// Constructs a ArrayType from a Json object
        /// </summary>
        /// <param name="json">The Json object used to construct a ArrayType</param>
        /// <returns>A new ArrayType instance</returns>
        public override sealed DataType FromJson(JObject json)
        {
            elementType = ParseDataTypeFromJson(json["elementType"]);
            containsNull = (bool)json["containsNull"];
            return this;
        }

        private DataType elementType;
        private bool containsNull;
    }

    /// <summary>
    /// The data type for Maps. Not implemented yet.
    /// </summary>
    [Serializable]
    public class MapType : ComplexType
    {
        public override object JsonValue
        {
            get { throw new NotImplementedException(); }
        }

        /// <summary>
        /// Constructs a StructField from a Json object. Not implemented yet.
        /// </summary>
        /// <param name="json">The Json object used to construct a MapType</param>
        /// <returns>A new MapType instance</returns>
        /// <exception cref="NotImplementedException"></exception>
        public override DataType FromJson(JObject json)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// A field inside a StructType.
    /// </summary>
    [Serializable]
    public class StructField : ComplexType
    {
        /// <summary>
        /// The name of this field.
        /// </summary>
        public string Name { get { return name; } }
        /// <summary>
        /// The data type of this field.
        /// </summary>
        public DataType DataType { get { return dataType; } }
        /// <summary>
        /// Indicates if values of this field can be null values.
        /// </summary>
        public bool IsNullable { get { return isNullable; } }
        /// <summary>
        /// The metadata of this field. The metadata should be preserved during transformation if the content of the column is not modified, e.g, in selection. 
        /// </summary>
        public JObject Metadata { get { return metadata; } }

        /// <summary>
        /// Initializes a StructField instance with a specific name, data type, nullable, and metadata
        /// </summary>
        /// <param name="name">The name of this field</param>
        /// <param name="dataType">The data type of this field</param>
        /// <param name="isNullable">Indicates if values of this field can be null values</param>
        /// <param name="metadata">The metadata of this field</param>
        public StructField(string name, DataType dataType, bool isNullable = true, JObject metadata = null)
        {
            this.name = name;
            this.dataType = dataType;
            this.isNullable = isNullable;
            this.metadata = metadata ?? new JObject();
        }

        public StructField(JObject json)
        {
            FromJson(json);
        }

        /// <summary>
        /// Returns a readable string that represents the type.
        /// </summary>
        public override string SimpleString { get { return string.Format(@"{0}:{1}", name, dataType.SimpleString); } }

        public override object JsonValue
        {
            get
            {
                return new JObject(
                            new JProperty("name", name),
                            new JProperty("type", dataType.JsonValue),
                            new JProperty("nullable", isNullable),
                            new JProperty("metadata", metadata));
            }
        }

        /// <summary>
        /// Constructs a StructField from a Json object
        /// </summary>
        /// <param name="json">The Json object used to construct a StructField</param>
        /// <returns>A new StructField instance</returns>
        public override sealed DataType FromJson(JObject json)
        {
            name = json["name"].ToString();
            dataType = ParseDataTypeFromJson(json["type"]);
            isNullable = (bool)json["nullable"];
            metadata = (JObject)json["metadata"];
            return this;
        }

        private string name;
        private DataType dataType;
        private bool isNullable;
        [NonSerialized]
        private JObject metadata;
    }

    /// <summary>
    /// Struct type, consisting of a list of StructField
    /// This is the data type representing a Row
    /// </summary>
    [Serializable]
    public class StructType : ComplexType
    {
        /// <summary>
        /// Gets a list of StructField.
        /// </summary>
        public List<StructField> Fields { get { return fields; } }

        public IStructTypeProxy StructTypeProxy
        {
            get
            {
                return structTypeProxy ?? 
                    new StructTypeIpcProxy(
                        new JvmObjectReference(SparkCLRIpcProxy.JvmBridge.CallStaticJavaMethod("org.apache.spark.sql.api.csharp.SQLUtils", "createSchema",
                            new object[] { Json }).ToString()));
            }
        }

        /// <summary>
        /// Initializes a StructType instance with a specific collection of SructField object.
        /// </summary>
        /// <param name="fields">The collection that holds StructField objects</param>
        public StructType(IEnumerable<StructField> fields)
        {
            this.fields = fields.ToList();
        }

        public StructType(JObject json)
        {
            FromJson(json);
        }

        public StructType(IStructTypeProxy structTypeProxy)
        {
            this.structTypeProxy = structTypeProxy;
            var jsonSchema = structTypeProxy.ToJson();
            FromJson(jsonSchema);
        }

        /// <summary>
        /// Returns a readable string that joins all <see cref="StructField"/>s together.
        /// </summary>
        public override string SimpleString
        {
            get { return string.Format(@"struct<{0}>", string.Join(",", fields.Select(f => f.SimpleString))); }
        }

        public override object JsonValue
        {
            get
            {
                return new JObject(
                                new JProperty("type", TypeName),
                                new JProperty("fields", fields.Select(f => f.JsonValue).ToArray()));
            }
        }

        /// <summary>
        /// Constructs a StructType from a Json object
        /// </summary>
        /// <param name="json">The Json object used to construct a StructType</param>
        /// <returns>A new StructType instance</returns>
        public override sealed DataType FromJson(JObject json)
        {
            var fieldsJObjects = json["fields"].Select(f => (JObject)f);
            fields = fieldsJObjects.Select(fieldJObject => (new StructField(fieldJObject))).ToList();
            return this;
        }

        [NonSerialized]
        private readonly IStructTypeProxy structTypeProxy;

        private List<StructField> fields;
    }

}
