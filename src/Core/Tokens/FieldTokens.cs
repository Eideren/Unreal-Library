using System;

namespace UELib.Core
{
    public partial class UStruct
    {
        public partial class UByteCodeDecompiler
        {
            public abstract class FieldToken : Token
            {
                public UObject Object{ get; private set; }
                public static UObject LastField{ get; internal set; }

                public override void Deserialize( IUnrealStream stream )
                {
                    Object = Decompiler._Container.TryGetIndexObject( stream.ReadObjectIndex() );
                    Decompiler.AlignObjectSize();
                }

                public override string Decompile()
                {
                    LastField = Object;
                    return Object != null ? Object.Name : "@NULL";
                }
            }

            public class NativeParameterToken : FieldToken
            {
                public override string Decompile()
                {
#if DEBUG
                    Decompiler._CanAddSemicolon = true;
                    Decompiler._MustCommentStatement = true;
                    return "native." + base.Decompile();
#else
                    return String.Empty;
#endif
                }
            }

            public class InstanceVariableToken : FieldToken{}
            public class LocalVariableToken : FieldToken{}
            public class StateVariableToken : FieldToken{}
            public class OutVariableToken : FieldToken{}

            public class DefaultVariableToken : FieldToken
            {
                public override string Decompile()
                {
                    for (var o = Object.Outer; o != null; o = o.Outer)
                    {
                        if (o is UClass)
                        {
                            return $"DefaultAs<{o.GetFriendlyType()}>()." + base.Decompile();
                        }
                    }
                    return "Default()." + base.Decompile();
                }
            }

            public class DynamicVariableToken : Token
            {
                protected int LocalIndex;

                public override void Deserialize( IUnrealStream stream )
                {
                    LocalIndex = stream.ReadInt32();
                    Decompiler.AlignSize( sizeof(int) );
                }

                public override string Decompile()
                {
                    return "UnknownLocal_" + LocalIndex;
                }
            }

            public class UndefinedVariableToken : Token
            {
                public override string Decompile()
                {
                    return String.Empty;
                }
            }

            public class DelegatePropertyToken : FieldToken
            {
                public int NameIndex;

                public override void Deserialize( IUnrealStream stream )
                {
                    // FIXME: MOHA or general?
                    if( stream.Version == 421 )
                    {
                        Decompiler.AlignSize( sizeof(int) );
                    }

                    // Unknown purpose.
                    NameIndex = stream.ReadNameIndex();
                    Decompiler.AlignNameSize();

                    // TODO: Corrigate version. Seen in version ~648(The Ball) may have been introduced earlier, but not prior 610.
                    if( stream.Version > 610 )
                    {
                        base.Deserialize( stream );
                    }
                }

                public override string Decompile()
                {
                    var v = Decompiler._Container.Package.GetIndexName( NameIndex );
                    return v == "None" ? "default" : v;
                }
            }

            public class DefaultParameterToken : Token
            {
                public override void Deserialize( IUnrealStream stream )
                {
                    stream.ReadUInt16();    // Size
                    Decompiler.AlignSize( sizeof(ushort) );

                    // FIXME: MOHA or general?
                    if( stream.Version == 421 )
                    {
                        Decompiler.AlignSize( sizeof(ushort) );
                    }

                    DeserializeNext();  // Expression
                    DeserializeNext();  // EndParmValue
                }

                public override string Decompile()
                {
                    string expression = DecompileNext();
                    DecompileNext();    // EndParmValue
                    Decompiler._CanAddSemicolon = true;

                    int paramIndex = 0;
                    foreach( var token in Decompiler.DeserializedTokens )
                    {
                        if( ReferenceEquals( token, this ) )
                            break;
                        if( token is DefaultParameterToken || token is NothingToken )
                            paramIndex++;
                    }

                    string paramName = null;
                    if( Decompiler._Container is UFunction f )
                    {
                        foreach( var param in f.Params )
                        {
                            if( param.HasPropertyFlag( Flags.PropertyFlagsLO.OptionalParm ) )
                            {
                                if( paramIndex == 0 )
                                {
                                    paramName = param.Name;
                                    if( param.HasPropertyFlag( Flags.PropertyFlagsLO.OutParm ) )
                                    {
                                        return $"#warning default assignment of out '{paramName}' to {expression}";
                                    }
                                    break;
                                }

                                paramIndex--;
                            }
                        }
                    }
                    paramName ??= "_UnknownOptionalParam_" + paramIndex;
                    return $"var {paramName} = _{paramName} ?? {expression}";
                }
            }

            public class BoolVariableToken : Token
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

            public class InstanceDelegateToken : Token
            {
                public override void Deserialize( IUnrealStream stream )
                {
                    stream.ReadNameIndex();
                    Decompiler.AlignNameSize();
                }
            }
        }
    }
}