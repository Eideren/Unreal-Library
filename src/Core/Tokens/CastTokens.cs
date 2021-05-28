namespace UELib.Core
{
    public partial class UStruct
    {
        public partial class UByteCodeDecompiler
        {
            public class CastToken : Token
            {
                public override void Deserialize( IUnrealStream stream )
                {
                    DeserializeNext();
                }

                public override string Decompile()
                {
                    return $"({DecompileNext()})";
                }
            }

            public class PrimitiveCastToken : Token
            {
                public override void Deserialize( IUnrealStream stream )
                {
                    DeserializeNext();
                }

                public override string Decompile()
                {
                    return DecompileNext();
                }
            }

            public abstract class ObjectCastToken : CastToken
            {
                public UObject CastedObject;

                public override void Deserialize( IUnrealStream stream )
                {
                    CastedObject = stream.ReadObject();
                    Decompiler.AlignObjectSize();

                    base.Deserialize( stream );
                }
            }

            public class DynamicCastToken : ObjectCastToken
            {
                public override string Decompile()
                {
                    var d = base.Decompile();
                    if (d == "default" || d == "(default)")
                        return $"(default({CastedObject.Name}))";
                    return $"({d} as {CastedObject.Name})";
                }
            }

            public class MetaCastToken : ObjectCastToken
            {
                public override string Decompile()
                {
                    return $"({base.Decompile()} as ClassT<{CastedObject.Name}>)";
                }
            }

            public class InterfaceCastToken : ObjectCastToken
            {
                public override string Decompile()
                {
                    var d = base.Decompile();
                    if (d == "default" || d == "(default)")
                        return $"(default({CastedObject.Name}))";
                    return $"({d} as {CastedObject.Name})";
                }
            }

            public class RotatorToVectorToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((Vector){base.Decompile()})";
                }
            }

            public class ByteToIntToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((int){DecompileNext()})";
                }
            }

            public class ByteToFloatToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((float){base.Decompile()})";
                }
            }

            public class ByteToBoolToken : CastToken
            {
                public override string Decompile()
                {
                    return $"ByteToBool({base.Decompile()})";
                }
            }

            public class IntToByteToken : CastToken
            {
                public override string Decompile()
                {
                    if(this.Decompiler.PreviousToken is InstanceVariableToken ivar && ivar.Object is UArrayProperty a && a.InnerProperty is UByteProperty b)
                        return $"(({b.EnumObject.GetFriendlyType()})(byte){base.Decompile()})";
                    else
                        return $"((byte){base.Decompile()})";
                }
            }

            public class IntToBoolToken : CastToken
            {
#if !SUPPRESS_BOOLINTEXPLOIT
                public override string Decompile()
                {
                    return $"((bool){base.Decompile()})";
                }
#endif
            }

            public class IntToFloatToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((float){base.Decompile()})";
                }
            }

            public class BoolToByteToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((byte){base.Decompile()})";
                }
            }

            public class BoolToIntToken : CastToken
            {
#if !SUPPRESS_BOOLINTEXPLOIT
                public override string Decompile()
                {
                    return $"((int){base.Decompile()})";
                }
#endif
            }

            public class BoolToFloatToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((float){base.Decompile()})";
                }
            }

            public class FloatToByteToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((byte){base.Decompile()})";
                }
            }

            public class FloatToIntToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((int){base.Decompile()})";
                }
            }

            public class FloatToBoolToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((bool){base.Decompile()})";
                }
            }

            public class ObjectToBoolToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((bool){base.Decompile()})";
                }
            }

            public class NameToBoolToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((bool){base.Decompile()})";
                }
            }

            public class StringToByteToken : CastToken
            {
                public override string Decompile()
                {
                    return $"StringToByte({base.Decompile()})";
                }
            }

            public class StringToIntToken : CastToken
            {
                public override string Decompile()
                {
                    return $"StringToInt({base.Decompile()})";
                }
            }

            public class StringToBoolToken : CastToken
            {
                public override string Decompile()
                {
                    return $"StringToBool({base.Decompile()})";
                }
            }

            public class StringToFloatToken : CastToken
            {
                public override string Decompile()
                {
                    return $"StringToFloat({base.Decompile()})";
                }
            }

            public class StringToVectorToken : CastToken
            {
                public override string Decompile()
                {
                    return $"StringToVector({base.Decompile()})";
                }
            }

            public class StringToRotatorToken : CastToken
            {
                public override string Decompile()
                {
                    return $"StringToRotator({base.Decompile()})";
                }
            }

            public class VectorToBoolToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((bool){base.Decompile()})";
                }
            }

            public class VectorToRotatorToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((Rotator){base.Decompile()})";
                }
            }

            public class RotatorToBoolToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((bool){base.Decompile()})";
                }
            }

            public class ByteToStringToken : CastToken
            {
                public override string Decompile()
                {
                    return $"({base.Decompile()}).ToString()";
                }
            }

            public class IntToStringToken : CastToken
            {
                public override string Decompile()
                {
                    return $"({base.Decompile()}).ToString()";
                }
            }

            public class BoolToStringToken : CastToken
            {
                public override string Decompile()
                {
                    return $"({base.Decompile()}).ToString()";
                }
            }

            public class FloatToStringToken : CastToken
            {
                public override string Decompile()
                {
                    return $"({base.Decompile()}).ToString()";
                }
            }

            public class NameToStringToken : CastToken
            {
                public override string Decompile()
                {
                    return $"({base.Decompile()}).ToString()";
                }
            }

            public class VectorToStringToken : CastToken
            {
                public override string Decompile()
                {
                    return $"({base.Decompile()}).ToString()";
                }
            }

            public class RotatorToStringToken : CastToken
            {
                public override string Decompile()
                {
                    return $"({base.Decompile()}).ToString()";
                }
            }

            public class StringToNameToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((name){base.Decompile()})";
                }
            }

            public class ObjectToStringToken : CastToken
            {
                public override string Decompile()
                {
                    return $"({base.Decompile()}).ToString()";
                }
            }

            public class InterfaceToStringToken : CastToken
            {
                public override string Decompile()
                {
                    return $"({base.Decompile()}).ToString()";
                }
            }

            public class InterfaceToBoolToken : CastToken
            {
                public override string Decompile()
                {
                    return $"((bool){base.Decompile()})";
                }
            }

            public class InterfaceToObjectToken : CastToken{}
            public class ObjectToInterfaceToken : CastToken{}

            public class DelegateToStringToken : CastToken
            {
                public override string Decompile()
                {
                    return $"({base.Decompile()}).ToString()";
                }
            }
        }
    }
}