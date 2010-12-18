using System;
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
        int TypeTag;
        public int Tag { get { return TypeTag; } }

        public abstract string TypeName();

        Type(int typeTag)
        {
            TypeTag = typeTag;
        }

        public abstract class ScalarType : Type
        {
            protected ScalarType(int typeTag)
                : base(typeTag)
            { }
        }

        public class BoolType : ScalarType
        {
            public BoolType()
                : base(++sTypeCounter)
            { }

            public override string TypeName()
            {
                return "bool";
            }
        }

        public class IntType : ScalarType
        {
            public IntType()
                : base(++sTypeCounter)
            { }

            public override string TypeName()
            {
                return "int";
            }
        }

        public class UIntType : ScalarType
        {
            public UIntType()
                : base(++sTypeCounter)
            { }

            public override string TypeName()
            {
                return "uint";
            }
        }

        public class FloatType : ScalarType
        {
            public FloatType()
                : base(++sTypeCounter)
            { }

            public override string TypeName()
            {
                return "float";
            }
        }

        public class SamplerType : Type
        {
            public SamplerType()
                : base(++sTypeCounter)
            { }

            public override string TypeName()
            {
                throw new NotImplementedException();
            }
        }

        public abstract class DerivedType : Type
        {
            protected DerivedType(int typeTag)
                : base(typeTag)
            { }
        }

        public class VectorType : DerivedType
        {
            public readonly Type BaseType;
            public readonly int Dimension;

            public VectorType(Type baseType, int dimension)
                : base(++sTypeCounter)
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

        public class MatrixType : DerivedType
        {
            public readonly Type BaseType;
            public readonly int Dimension;

            public MatrixType(Type baseType, int dimension)
                : base(++sTypeCounter)
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

        public class StructType : DerivedType
        {
            public readonly string Name;
            public readonly StructField[] Fields;

            public StructType(string name, StructField[] fields)
                : base(++sTypeCounter)
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

        static int sTypeCounter = 0;

        static BoolType sBoolType = new BoolType();
        static IntType sIntType = new IntType();
        static UIntType sUIntType = new UIntType();
        static FloatType sFloatType = new FloatType();
        static SamplerType sSamplerType = new SamplerType();

        static List<VectorType> sVectorTypes = new List<VectorType>();
        static List<MatrixType> sMatrixTypes = new List<MatrixType>();
        static List<StructType> sStructTypes = new List<StructType>();

        public static BoolType GetBoolType() { return sBoolType; }
        public static IntType GetIntType() { return sIntType; }
        public static UIntType GetUIntType() { return sUIntType; }
        public static FloatType GetFloatType() { return sFloatType; }
        public static SamplerType GetSamplerType() { return sSamplerType; }

        public static VectorType GetVectorType(Type baseType, int dimension)
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

        public static MatrixType GetMatrixType(Type baseType, int dimension)
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

        public static StructType GetStructType(string name)
        {
            foreach (StructType ST in sStructTypes)
            {
                if (ST.Name == name)
                    return ST;
            }

            return null;
        }

        public static StructType GetStructType(string name, StructField[] fields)
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

        public static ReadOnlyCollection<StructType> GetAllStructTypes()
        {
            return new ReadOnlyCollection<StructType>(sStructTypes);
        }
    }
}
