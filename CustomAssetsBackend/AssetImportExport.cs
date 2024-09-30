using AssetsTools.NET;
using Newtonsoft.Json.Linq;

namespace CustomAssetsBackend;

public class AssetImportExport
{
    private AssetsFileWriter aw;

    public void DumpJsonAsset(StreamWriter sw, AssetTypeValueField baseField)
    {
        JToken jBaseField = RecurseJsonDump(baseField, false);
        sw.Write(jBaseField.ToString());
    }

    private JToken RecurseJsonDump(AssetTypeValueField field, bool uabeFlavor)
    {
        AssetTypeTemplateField template = field.TemplateField;

        bool isArray = template.IsArray;

        if (isArray)
        {
            JArray jArray = new JArray();

            if (template.ValueType != AssetValueType.ByteArray)
            {
                for (int i = 0; i < field.Children.Count; i++)
                {
                    jArray.Add(RecurseJsonDump(field.Children[i], uabeFlavor));
                }
            }
            else
            {
                byte[] byteArrayData = field.AsByteArray;
                for (int i = 0; i < byteArrayData.Length; i++)
                {
                    jArray.Add(byteArrayData[i]);
                }
            }

            return jArray;
        }
        else
        {
            if (field.Value != null)
            {
                AssetValueType evt = field.Value.ValueType;
                
                if (field.Value.ValueType != AssetValueType.ManagedReferencesRegistry)
                {
                    object value = evt switch
                    {
                        AssetValueType.Bool => field.AsBool,
                        AssetValueType.Int8 or
                        AssetValueType.Int16 or
                        AssetValueType.Int32 => field.AsInt,
                        AssetValueType.Int64 => field.AsLong,
                        AssetValueType.UInt8 or
                        AssetValueType.UInt16 or
                        AssetValueType.UInt32 => field.AsUInt,
                        AssetValueType.UInt64 => field.AsULong,
                        AssetValueType.String => field.AsString,
                        AssetValueType.Float => field.AsFloat,
                        AssetValueType.Double => field.AsDouble,
                        _ => "invalid value"
                    };

                    return (JValue)JToken.FromObject(value);
                }
                else
                {
                    // todo separate method
                    ManagedReferencesRegistry registry = field.Value.AsManagedReferencesRegistry;

                    if (registry.version == 1 || registry.version == 2)
                    {
                        JArray jArrayRefs = new JArray();

                        foreach (AssetTypeReferencedObject refObj in registry.references)
                        {
                            AssetTypeReference typeRef = refObj.type;

                            JObject jObjManagedType = new JObject
                            {
                                { "class", typeRef.ClassName },
                                { "ns", typeRef.Namespace },
                                { "asm", typeRef.AsmName }
                            };

                            JObject jObjData = new JObject();

                            foreach (AssetTypeValueField child in refObj.data)
                            {
                                jObjData.Add(child.FieldName, RecurseJsonDump(child, uabeFlavor));
                            }

                            JObject jObjRefObject;

                            if (registry.version == 1)
                            {
                                jObjRefObject = new JObject
                                {
                                    { "type", jObjManagedType },
                                    { "data", jObjData }
                                };
                            }
                            else
                            {
                                jObjRefObject = new JObject
                                {
                                    { "rid", refObj.rid },
                                    { "type", jObjManagedType },
                                    { "data", jObjData }
                                };
                            }

                            jArrayRefs.Add(jObjRefObject);
                        }

                        JObject jObjReferences = new JObject
                        {
                            { "version", registry.version },
                            { "RefIds", jArrayRefs }
                        };

                        return jObjReferences;
                    }
                    else
                    {
                        throw new NotSupportedException($"Registry version {registry.version} not supported!");
                    }
                }
            }
            else
            {
                JObject jObject = new JObject();

                foreach (AssetTypeValueField child in field)
                {
                    jObject.Add(child.FieldName, RecurseJsonDump(child, uabeFlavor));
                }

                return jObject;
            }
        }
    }

    public byte[]? ImportJsonAsset(AssetTypeTemplateField tempField, StreamReader sr, out Exception? err)
    {
        using (MemoryStream ms = new MemoryStream())
        {
            aw = new AssetsFileWriter(ms);
            aw.BigEndian = false;

            try
            {
                string jsonText = sr.ReadToEnd();
                JToken token = JToken.Parse(jsonText);

                RecurseJsonImport(tempField, token);
                err = null;
            }
            catch (Exception ex)
            {
                err = ex;
                return null;
            }
            return ms.ToArray();
        }
    }

    private void RecurseJsonImport(AssetTypeTemplateField tempField, JToken token)
    {
        bool align = tempField.IsAligned;

        if (!tempField.HasValue && !tempField.IsArray)
        {
            foreach (AssetTypeTemplateField childTempField in tempField.Children)
            {
                JToken? childToken = token[childTempField.Name];

                if (childToken == null)
                {
                    if (tempField != null)
                    {
                        throw new Exception($"Missing field {childTempField.Name} in JSON. Parent field is {tempField.Type} {tempField.Name}.");
                    }
                    else
                    {
                        throw new Exception($"Missing field {childTempField.Name} in JSON.");
                    }
                }
                    
                RecurseJsonImport(childTempField, childToken);
            }

            if (align)
            {
                aw.Align();
            }
        }
        else if (tempField.HasValue && tempField.ValueType == AssetValueType.ManagedReferencesRegistry)
        {
            throw new NotImplementedException("SerializeReference not supported in JSON import yet!");
        }
        else
        {
            switch (tempField.ValueType)
            {
                case AssetValueType.Bool:
                {
                    aw.Write((bool)token);
                    break;
                }
                case AssetValueType.UInt8:
                {
                    aw.Write((byte)token);
                    break;
                }
                case AssetValueType.Int8:
                {
                    aw.Write((sbyte)token);
                    break;
                }
                case AssetValueType.UInt16:
                {
                    aw.Write((ushort)token);
                    break;
                }
                case AssetValueType.Int16:
                {
                    aw.Write((short)token);
                    break;
                }
                case AssetValueType.UInt32:
                {
                    aw.Write((uint)token);
                    break;
                }
                case AssetValueType.Int32:
                {
                    aw.Write((int)token);
                    break;
                }
                case AssetValueType.UInt64:
                {
                    aw.Write((ulong)token);
                    break;
                }
                case AssetValueType.Int64:
                {
                    aw.Write((long)token);
                    break;
                }
                case AssetValueType.Float:
                {
                    aw.Write((float)token);
                    break;
                }
                case AssetValueType.Double:
                {
                    aw.Write((double)token);
                    break;
                }
                case AssetValueType.String:
                {
                    align = true;
                    aw.WriteCountStringInt32((string?)token ?? "");
                    break;
                }
                case AssetValueType.ByteArray:
                {
                    JArray byteArrayJArray = ((JArray?)token) ?? new JArray();
                    byte[] byteArrayData = new byte[byteArrayJArray.Count];
                    for (int i = 0; i < byteArrayJArray.Count; i++)
                    {
                        byteArrayData[i] = (byte)byteArrayJArray[i];
                    }
                    aw.Write(byteArrayData.Length);
                    aw.Write(byteArrayData);
                    break;
                }
            }

            // have to do this because of bug in MonoDeserializer
            if (tempField.IsArray && tempField.ValueType != AssetValueType.ByteArray)
            {
                // children[0] is size field, children[1] is the data field
                AssetTypeTemplateField childTempField = tempField.Children[1];

                JArray? tokenArray = (JArray?)token;

                if (tokenArray == null)
                    throw new Exception($"Field {tempField.Name} was not an array in json.");

                aw.Write(tokenArray.Count);
                foreach (JToken childToken in tokenArray.Children())
                {
                    RecurseJsonImport(childTempField, childToken);
                }
            }

            if (align)
            {
                aw.Align();
            }
        }
    }
}