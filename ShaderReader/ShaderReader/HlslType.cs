﻿using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Hlsl
{
    class StructField
    {
        public StructField(Type type, string name, Semantic semantic)
        {
            FieldType = type;
            FieldName = name;
            FieldSemantic = semantic;
        }

        public Type FieldType;
        public string FieldName;
        public Semantic FieldSemantic;

        public static bool operator ==(StructField A, StructField B)
        {
            if (A.FieldType == B.FieldType 
                && A.FieldName == B.FieldName 
                && A.FieldSemantic == B.FieldSemantic)
                return true;

            return false;
        }

        public static bool operator !=(StructField A, StructField B)
        {
            return !(A == B);
        }

        public override bool Equals(object obj)
        {
            StructField SF = obj as StructField;
            if (SF != null)
                return SF == this;

            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    abstract class Type
    {
        public abstract string TypeName();

        protected Type() { }

        public static bool operator ==(Type A, Type B)
        {
            object a = (object)A;
            object b = (object)B;

            if (a == null && b == null)
                return true;

            if (a == null || b == null)
                return false;

            return A.GetType() == B.GetType();
        }

        public static bool operator !=(Type A, Type B)
        {
            return !(A == B);
        }

        public override bool Equals(object obj)
        {
            return this == (Type)obj;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    abstract class ScalarType : Type
    {
        protected ScalarType() { }
    }

    class BoolType : ScalarType
    {
        public BoolType() { }

        public override string TypeName()
        {
            return "bool";
        }
    }

    class IntType : ScalarType
    {
        public IntType() { }

        public override string TypeName()
        {
            return "int";
        }
    }

    class UIntType : ScalarType
    {
        public UIntType() { }

        public override string TypeName()
        {
            return "uint";
        }
    }

    class FloatType : ScalarType
    {
        public FloatType() { }

        public override string TypeName()
        {
            return "float";
        }
    }

    class SamplerType : Type
    {
        public SamplerType() { }

        public override string TypeName()
        {
            throw new NotImplementedException();
        }
    }

    abstract class DerivedType : Type
    {
        protected DerivedType() { }
    }

    class VectorType : DerivedType
    {
        public readonly Type BaseType;
        public readonly int Dimension;

        public VectorType(Type baseType, int dimension)
        {
            if (!(baseType is ScalarType))
                throw new ShaderDomException("Vector base type must be a scalar!");

            BaseType = baseType;
            Dimension = dimension;
        }

        public override string TypeName()
        {
            return BaseType.TypeName() + Dimension.ToString();
        }
    }

    class MatrixType : DerivedType
    {
        public readonly Type BaseType;
        public readonly int Dimension;

        public MatrixType(Type baseType, int dimension)
        {
            if (!(baseType is VectorType))
                throw new ShaderDomException("Matrix base type must be a vector!");

            BaseType = baseType;
            Dimension = dimension;
        }

        public override string TypeName()
        {
            return BaseType.TypeName() + "x" + Dimension.ToString();
        }
    }

    class StructType : DerivedType
    {
        public readonly string Name;
        public readonly StructField[] Fields;

        public StructType(string name, StructField[] fields)
        {
            Name = name;
            Fields = fields;
        }

        public override string TypeName()
        {
            return Name;
        }

        public override string ToString()
        {
            StringBuilder SB = new StringBuilder();

            SB.AppendFormat("struct {0} {{{1}", Name, System.Environment.NewLine);

            foreach (StructField SF in Fields)
                SB.AppendFormat("    {0} {1} : {2};{3}",
                    SF.FieldType.TypeName(),
                    SF.FieldName,
                    SF.FieldSemantic,
                    System.Environment.NewLine);

            SB.AppendLine("};");

            return SB.ToString();
        }
    }

    class TypeRegistry
    {
        BoolType sBoolType = new BoolType();
        IntType sIntType = new IntType();
        UIntType sUIntType = new UIntType();
        FloatType sFloatType = new FloatType();
        SamplerType sSamplerType = new SamplerType();

        List<VectorType> sVectorTypes = new List<VectorType>();
        List<MatrixType> sMatrixTypes = new List<MatrixType>();
        List<StructType> sStructTypes = new List<StructType>();

        public BoolType GetBoolType() { return sBoolType; }
        public IntType GetIntType() { return sIntType; }
        public UIntType GetUIntType() { return sUIntType; }
        public FloatType GetFloatType() { return sFloatType; }
        public SamplerType GetSamplerType() { return sSamplerType; }

        public VectorType GetVectorType(Type baseType, int dimension)
        {
            foreach (VectorType VT in sVectorTypes)
            {
                if (VT.BaseType == baseType && VT.Dimension == dimension)
                    return VT;
            }

            VectorType newVT = new VectorType(baseType, dimension);
            sVectorTypes.Add(newVT);

            return newVT;
        }

        public MatrixType GetMatrixType(Type baseType, int dimension)
        {
            foreach (MatrixType MT in sMatrixTypes)
            {
                if (MT.BaseType == baseType && MT.Dimension == dimension)
                    return MT;
            }

            MatrixType newVT = new MatrixType(baseType, dimension);
            sMatrixTypes.Add(newVT);

            return newVT;
        }

        public StructType GetStructType(string name)
        {
            foreach (StructType ST in sStructTypes)
            {
                if (ST.Name == name)
                    return ST;
            }

            return null;
        }

        public StructType GetStructType(string name, StructField[] fields)
        {
            StructType ST = GetStructType(name);

            if (ST != null)
            {
                bool allFieldsMatch = ST.Fields.Length == fields.Length;

                if (allFieldsMatch)
                {
                    for (int i = 0; i < ST.Fields.Length; ++i)
                    {
                        if (ST.Fields[i] != fields[i])
                        {
                            allFieldsMatch = false;
                            break;
                        }
                    }
                }

                if (allFieldsMatch)
                    return ST;
                else
                    throw new ShaderDomException("Redefinition of existing struct type!");
            }

            StructType newST = new StructType(name, fields);
            sStructTypes.Add(newST);

            return newST;
        }

        public ReadOnlyCollection<StructType> GetAllStructTypes()
        {
            return new ReadOnlyCollection<StructType>(sStructTypes);
        }
    }
}
