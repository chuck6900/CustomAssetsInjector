﻿using LibCpp2IL.Metadata;
using LibCpp2IL.Reflection;

namespace AssetsTools.NET.Cpp2IL
{
    internal struct TypeDefWithSelfRef
    {
        public Il2CppTypeReflectionData typeRef;
        public Il2CppTypeDefinition typeDef;

        public Dictionary<string, TypeDefWithSelfRef> typeParamToArg;
        public string[] paramNames;

        public TypeDefWithSelfRef(Il2CppTypeReflectionData typeRef)
        {
            this.typeRef = typeRef;
            typeDef = typeRef.Resolve();
            typeParamToArg = new Dictionary<string, TypeDefWithSelfRef>();
            paramNames = Array.Empty<string>();

            Il2CppTypeReflectionData tRef = typeRef;

            if (typeRef.isArray)
            {
                tRef = typeRef.arrayType;
            }

            if (tRef.isGenericType)
            {
                paramNames = tRef.baseType.GenericContainer.GenericParameters.Select(p => p.Name).ToArray();
                for (int i = 0; i < tRef.genericParams.Length; i++)
                {
                    typeParamToArg.Add(paramNames[i], new TypeDefWithSelfRef(tRef.genericParams[i]));
                }
            }
        }

        public void AssignTypeParams(TypeDefWithSelfRef parentTypeDef)
        {
            if (parentTypeDef.typeParamToArg.Count > 0 && typeRef.isGenericType)
            {
                for (int i = 0; i < typeRef.genericParams.Length; i++)
                {
                    TypeDefWithSelfRef genTypeRef = new TypeDefWithSelfRef(typeRef.genericParams[i]);
                    if (!string.IsNullOrEmpty(genTypeRef.typeRef.variableGenericParamName))
                    {
                        if (parentTypeDef.typeParamToArg.TryGetValue(genTypeRef.typeRef.variableGenericParamName, out TypeDefWithSelfRef mappedType))
                        {
                            typeParamToArg[paramNames[i]] = mappedType;
                        }
                    }
                    else
                    {
                        // nested generic, AssignTypeParams should get it
                        genTypeRef.AssignTypeParams(parentTypeDef);
                        typeParamToArg[paramNames[i]] = genTypeRef;
                    }
                }
            }
        }

        public TypeDefWithSelfRef SolidifyType(TypeDefWithSelfRef typeDef)
        {
            typeDef.AssignTypeParams(this);
            if (typeParamToArg.TryGetValue(typeDef.typeRef.ToString(), out TypeDefWithSelfRef retType))
            {
                return retType;
            }

            return typeDef;
        }

        public static implicit operator TypeDefWithSelfRef(Il2CppTypeReflectionData typeReference)
        {
            return new TypeDefWithSelfRef(typeReference);
        }
    }
}