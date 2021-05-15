using System;
using System.Collections.Generic;
using System.Globalization;

namespace UELib.Core
{
    public partial class UStruct
    {
        public partial class UByteCodeDecompiler
        {
            public class IntZeroToken : Token
            {
                public override string Decompile()
                {
                     return "0";
                }
            }

            public class IntOneToken : Token
            {
                public override string Decompile()
                {
                     return "1";
                }
            }

            public class TrueToken : Token
            {
                public override string Decompile()
                {
                     return "true";
                }
            }

            public class FalseToken : Token
            {
                public override string Decompile()
                {
                     return "false";
                }
            }

            public class NoneToken : Token
            {
                public override string Decompile()
                {
                     return "default";
                }
            }

            public class SelfToken : Token
            {
                public override string Decompile()
                {
                     return "this";
                }
            }

            public class IntConstToken : Token
            {
                public int Value{ get; private set; }

                public override void Deserialize( IUnrealStream stream )
                {
                    Value = stream.ReadInt32();
                    Decompiler.AlignSize( sizeof(int) );
                }

                public override string Decompile()
                {
                    return String.Format( "{0}", Value );
                }
            }

            public class ByteConstToken : Token
            {
                public byte Value{ get; private set; }

                private enum ENetRole
                {
                    ROLE_None = 0,
                    ROLE_DumbProxy = 1,
                    ROLE_SimulatedProxy = 2,
                    ROLE_AutonomousProxy = 3,
                    ROLE_Authority = 4
                }

                private enum ENetRole3
                {
                    ROLE_None = 0,
                    ROLE_SimulatedProxy = 1,
                    ROLE_AutonomousProxy = 2,
                    ROLE_Authority = 3,
                    ROLE_MAX = 4
                }

                private enum ENetMode
                {
                    NM_Standalone = 0,
                    NM_DedicatedServer = 1,
                    NM_ListenServer = 2,
                    NM_Client = 3,
                    NM_MAX = 4
                }

                public override void Deserialize( IUnrealStream stream )
                {
                    Value = stream.ReadByte();
                    Decompiler.AlignSize( sizeof(byte) );
                }


                
                bool FindEnum(out UEnum uEnum, out string debugError)
                {
                    var tokens = Decompiler.DeserializedTokens;
                    var currToken = Decompiler.CurrentTokenIndex;


                    uEnum = null;
                    debugError = null;
                    if (tokens[currToken - 1] is ByteToIntToken)
                    {
                        // Comparison or assignment between enum field and enum
                        // Comparison between returned enum from function and enum
                        // Assignment to function parameter
                        uEnum = InnerFind(tokens[currToken - 2], tokens, out debugError)?.EnumObject;
                    }

                    // This is the value for a 'case:', try to find the switch's param value and parse this value to that param's type
                    if (uEnum == null && Decompiler.PreviousToken is CaseToken ct && ct.OwnerHack.ParamHack is Token token)
                    {
                        uEnum = InnerFind(token, tokens, out debugError)?.EnumObject;
                    }
                    return uEnum != null;


                    static UByteProperty InnerFind(Token token, List<Token> tokens, out string debugError)
                    {
                        debugError = null;
                        if (token is ContextToken ctx)
                        {
                            // Find where this context sits within the tokens to find where the actual field referenced by this context is
                            // Probably breaks if there is a context chain 
                            int index;
                            for (index = 0; index < tokens.Count; index++)
                            {
                                if (ReferenceEquals(tokens[index], ctx))
                                    break;
                            }

                            token = tokens[index + 2];
                        }
                        
                        // Comparison or assignment between enum field and enum
                        if (token is FieldToken field)
                            return field.Object as UByteProperty;
                        else if (token is StructMemberToken structMember)
                            return structMember.MemberProperty as UByteProperty;
                        // Comparison between returned enum from function and enum
                        else if (token is EndFunctionParmsToken e)
                        {
                            // Find index of this end token
                            int index = -1;
                            FunctionToken f;
                            // Scan all tokens to find this end token
                            while (ReferenceEquals(tokens[++index], e) == false) { }

                            // Now scan backwards from there until we stumble upon the start of this end token
                            while ((f = tokens[--index] as FunctionToken) == null) { }

                            if (f is FinalFunctionToken ft)
                                return ft.Function.ReturnProperty as UByteProperty;
                            else if(f.TryFindFunction() is UFunction rf)
                                return rf.ReturnProperty as UByteProperty;
                            debugError = "/*unsupported function token*/";
                            return null; // Would have to find this specific method from just the name to figure out what the return type is
                        }
                        else if (token is FunctionToken f)
                        {
                            if(f.TryFindFunction() is UFunction rf)
                                return rf.ReturnProperty as UByteProperty;
                            debugError = "/*unsupported function token*/";
                            return null; // Would have to find this specific method from just the name to figure out what the return type is 
                        }
                        else if (token is DynamicArrayElementToken)
                            return null; // not implemented yet
                        
                        System.Diagnostics.Debugger.Break();
                        return null;
                    }
                }


                
                public override string Decompile()
                {
                    if (FindEnum(out var enumObject, out var errorInfo))
                    {
                        var index = Value;
                        if (index < enumObject.Names.Count)
                            return $"{enumObject.GetFriendlyType()}.{enumObject.Names[index]}/*{Value}*/";
                        else
                            return $"/*val out of enum range*/{Value}";
                    }

                    return errorInfo + Value.ToString(CultureInfo.InvariantCulture);
                }
            }

            public class IntConstByteToken : Token
            {
                public byte Value{ get; private set; }

                public override void Deserialize( IUnrealStream stream )
                {
                    Value = stream.ReadByte();
                    Decompiler.AlignSize( sizeof(byte) );
                }

                public override string Decompile()
                {
                    return String.Format( "{0:d}", Value );
                }
            }

            public class FloatConstToken : Token
            {
                public float Value{ get; private set; }

                public override void Deserialize( IUnrealStream stream )
                {
                    Value = stream.UR.ReadSingle();
                    Decompiler.AlignSize( sizeof(float) );
                }

                public override string Decompile()
                {
                    return Value.ToUFloat();
                }
            }

            public class ObjectConstToken : Token
            {
                public int ObjectIndex{ get; private set; }

                public override void Deserialize( IUnrealStream stream )
                {
                    ObjectIndex = stream.ReadObjectIndex();
                    Decompiler.AlignObjectSize();
                }

                public override string Decompile()
                {
                    UObject obj = Decompiler._Container.GetIndexObject( ObjectIndex );
                    if( obj != null )
                    {
                        var nextToken = Decompiler.PeekToken;
                        
                        // class'objectclasshere'
                        string className = obj.GetClassName();
                        
                        if( className == "Class" || String.IsNullOrEmpty( className ) )
                        {
                            if (nextToken is FunctionToken && Decompiler._IsWithinClassContext != ClassContext.No )
                            {
                                // This is the type that implements the static function we're going to call,
                                // we just need the type name in this case and can omit 'Static.'
                                Decompiler._IsWithinClassContext = ClassContext.TypeContext;
                                return obj.Name;
                            } 
                            else
                                return $"ClassT<{obj.Name}>()";
                        }
                        return $"ObjectConst<{className}>(\"{obj.Name}\")";
                    }
                    return "default";
                }
            }

            public class NameConstToken : Token
            {
                public int NameIndex{ get; private set; }

                public override void Deserialize( IUnrealStream stream )
                {
                    NameIndex = stream.ReadNameIndex();
                    Decompiler.AlignNameSize();
                }

                public override string Decompile()
                {
                    return "\"" + Decompiler._Container.Package.GetIndexName( NameIndex ) + "\"";
                }
            }

            public class StringConstToken : Token
            {
                public string Value{ get; private set; }

                public override void Deserialize( IUnrealStream stream )
                {
                    Value = stream.UR.ReadAnsi();
                    Decompiler.AlignSize( Value.Length + 1 );   // inc null char
                }

                public override string Decompile()
                {
                    return "\"" + Value.Escape() + "\"";
                }
            }

            public class UniStringConstToken : Token
            {
                public string Value{ get; private set; }

                public override void Deserialize( IUnrealStream stream )
                {
                    Value = stream.UR.ReadUnicode();
                    Decompiler.AlignSize( (Value.Length * 2) + 2 ); // inc null char
                }

                public override string Decompile()
                {
                    return "\"" + Value.Escape() + "\"";
                }
            }

            public class RotatorConstToken : Token
            {
                public struct Rotator
                {
                    public int Pitch, Yaw, Roll;
                }

                public Rotator Value;

                public override void Deserialize( IUnrealStream stream )
                {
                    Value.Pitch = stream.ReadInt32();
                    Decompiler.AlignSize( sizeof(int) );
                    Value.Yaw = stream.ReadInt32();
                    Decompiler.AlignSize( sizeof(int) );
                    Value.Roll = stream.ReadInt32();
                    Decompiler.AlignSize( sizeof(int) );
                }

                public override string Decompile()
                {
                    return "rot(" + Value.Pitch + ", " + Value.Yaw + ", " + Value.Roll + ")";
                }
            }

            public class VectorConstToken : Token
            {
                public float X, Y, Z;

                public override void Deserialize( IUnrealStream stream )
                {
                    X = stream.UR.ReadSingle();
                    Decompiler.AlignSize( sizeof(float) );
                    Y = stream.UR.ReadSingle();
                    Decompiler.AlignSize( sizeof(float) );
                    Z = stream.UR.ReadSingle();
                    Decompiler.AlignSize( sizeof(float) );
                }

                public override string Decompile()
                {
                    return String.Format( "vect({0}, {1}, {2})", X.ToUFloat(), Y.ToUFloat(), Z.ToUFloat() );
                }
            }
        }
    }
}