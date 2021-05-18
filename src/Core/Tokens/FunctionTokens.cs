using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UELib.Core
{
    public partial class UStruct
    {
        public partial class UByteCodeDecompiler
        {
            public class EndFunctionParmsToken : Token
            {
                public override string Decompile()
                {
                    return ")";
                }
            }

            public abstract class FunctionToken : Token
            {
                private static int stacking;
                public UFunction TryFindFunction(int paramCount = -1)
                {
                    int thisTokenIndex = -1;
                    var tokens = Decompiler.DeserializedTokens;
                    while (ReferenceEquals(tokens[++thisTokenIndex], this) == false) { }

                    if (paramCount == -1)
                    {
                        paramCount = 0;
                        while (tokens[++thisTokenIndex] is var t && t is EndFunctionParmsToken == false)
                        {
                            if (t is ContextToken 
                                || t is ArrayElementToken)
                            {
                                paramCount -= 1;
                                continue;
                            }
                            else if (t is ConditionalToken)
                            {
                                paramCount -= 2;
                                continue;
                            }
                            else if (t is FunctionToken)
                            {
                                int depth = 1;
                                do
                                {
                                    Token t2 = tokens[++thisTokenIndex];
                                    if (t2 is EndFunctionParmsToken)
                                        depth--;
                                    else if (t2 is FunctionToken)
                                        depth++;
                                } while (depth != 0);
                                paramCount++;
                            }
                            else if (t is CastToken 
                                     || t is BoolVariableToken
                                     || t is StructMemberToken
                                     || t is DynamicArrayLengthToken)
                                continue;
                            else
                                paramCount++;
                        }
                    }
                    
                    var fName = FunctionName;
                    
                    foreach (var f in (
                        from p in UnrealConfig.SharedPackages
                        from o in p.Exports
                        where o.Object is UState
                        from f in ((UState)o.Object).Functions
                        where f.Name == fName
                        select f
                    ))
                    {
                        if (f.Params.Count - (f.ReturnProperty == null ? 0 : 1) == paramCount)
                            return f;
                    }

                    return null; // Most likely failed to properly parse the amount of parameters provided to the function
                }

                public abstract string FunctionName { get; }



                protected void DeserializeCall()
                {
                    DeserializeParms();
                    Decompiler.DeserializeDebugToken();
                }

                private void DeserializeParms()
                {
#pragma warning disable 642
                    while( !(DeserializeNext() is EndFunctionParmsToken) );
#pragma warning restore 642
                }

                protected void DeserializeBinaryOperator()
                {
                    DeserializeNext();
                    DeserializeNext();

                    DeserializeNext(); // )
                    Decompiler.DeserializeDebugToken();
                }

                protected void DeserializeUnaryOperator()
                {
                    DeserializeNext();

                    DeserializeNext(); // )
                    Decompiler.DeserializeDebugToken();
                }

                private static string PrecedenceToken( Token t )
                {
                    if( !(t is FunctionToken) )
                        return t.Decompile();

                    // Always add ( and ) unless the conditions below are not met, in case of a VirtualFunctionCall.
                    var addParenthesises = true;
                    if( t is NativeFunctionToken )
                    {
                        addParenthesises = ((NativeFunctionToken)t).NativeTable.Type == FunctionType.Operator;
                    }
                    else if( t is FinalFunctionToken )
                    {
                        addParenthesises = ((FinalFunctionToken)t).Function.IsOperator();
                    }
                    return addParenthesises ? String.Format( "({0})", t.Decompile() ) : t.Decompile();
                }

                protected string DecompilePreOperator( string operatorName )
                {
                    string output = operatorName + (operatorName.Length > 1 ? " " : String.Empty) + DecompileNext();
                    DecompileNext(); // )
                    return output;
                }

                protected string DecompileOperator( string operatorName )
                {
                    string output;
                    
                    var left = GrabNextToken();
                    var rLeft = PrecedenceToken(left);
                    var right = GrabNextToken();
                    var rRight = PrecedenceToken(right);
                    DecompileNext(); // )

                    var target = left.TryGetAssociatedFieldToken();
                    var src = right.TryGetAssociatedFieldToken();

                    operatorName = operatorName switch
                    {
                        "$" => "+", // string concat
                        "$=" => "+=", // string concat
                        "@" => "+ \" \" +", // spaced string concat
                        "@=" => "+= \" \" +", // spaced string concat
                        "~=" => "ApproximatelyEqual",
                        "**" => "Exponentiation",
                        "<<" => "/*<<*/ShiftL",
                        ">>" => "/*>>*/ShiftR",
                        "Percent_IntInt" => "%",
                        _ => operatorName
                    };

                    bool asFunction = true;
                    for (int i = 0; i < operatorName.Length; i++)
                    {
                        // Skip comments
                        if (operatorName[i] == '/' && i + 1 < operatorName.Length && operatorName[i + 1] == '*')
                        {
                            for (i += 2; i < operatorName.Length; i++)
                            {
                                if (operatorName[i] == '*' && i + 1 < operatorName.Length && operatorName[i] == '/')
                                {
                                    i += 2;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            var c = operatorName[i];
                            if (c == '_' || char.IsLetter(c))
                                continue;
                            asFunction = false;
                        }
                    }
                    
                    if (operatorName == "*=" && target?.GetFriendlyType() == "int" && (right is FloatConstToken || src?.GetFriendlyType() == "float"))
                    {
                        return $"{rLeft} = /*initially 'intX *= floatY' */IntFloat_Mult({rLeft}, {rRight})";
                    }
                    else if (asFunction)
                    {
                        return $"{operatorName}({rLeft}, {rRight})"; 
                    }
                    else
                    {
                        return $"{rLeft} {operatorName} {rRight}";
                    }
                }

                protected string DecompilePostOperator( string operatorName )
                {
                    string output = operatorName + " " + DecompileNext();
                    DecompileNext(); // )
                    return output;
                }

                protected string DecompileCall( string functionName )
                {
                    string isStatic = "";
                    if( Decompiler._IsWithinClassContext == ClassContext.Class )
                    {
                        isStatic = "Static.";

                        // Set false elsewhere as well but to be sure we set it to false here to avoid getting static calls inside the params.
                        // e.g.
                        // A1233343.DrawText(Class'BTClient_Interaction'.static.A1233332(static.Max(0, A1233328 - A1233322[A1233222].StartTime)), true);
                    }
                    Decompiler._IsWithinClassContext = ClassContext.No;
                    return isStatic + functionName + "(" + DecompileParms();
                }

                public List<string> ParamsHack = new List<string>();

                private string DecompileParms()
                {
                    var tokens = new List<(Token token, String output, UProperty prop)>();
                    int paramCount = 0;
                    {
                    next:
                        var t = GrabNextToken();
                        tokens.Add( ( t, t.Decompile(), null ) );
                        if (!(t is EndFunctionParmsToken))
                        {
                            paramCount++;
                            goto next;
                        }
                    }

                    UFunction matchingFunction;
                    if (this is FinalFunctionToken fft)
                    {
                        matchingFunction = fft.Function;
                    }
                    else
                    {
                        // Try to find matching function to fix ref stuff
                        matchingFunction = TryFindFunction(paramCount);
                    }

                    if (matchingFunction != null)
                    {
                        int index = 0;
                        foreach (var param in matchingFunction.Params)
                        {
                            if (param == matchingFunction.ReturnProperty)
                                continue;
                            var p = tokens[index];
                            tokens[index] = (p.token, p.output, param);
                            index++;
                        }
                    }

                    var output = new StringBuilder();
                    ParamsHack.Clear();
                    for( int i = 0; i < tokens.Count; ++ i )
                    {
                        var (t, v, p) = tokens[i];

                        if( t is EndFunctionParmsToken ) // End ")"
                        {
                            output = new StringBuilder( output.ToString().TrimEnd( ',' ) + v );
                        }
                        else // Any passed values
                        {
                            if( i != tokens.Count - 1 && i > 0 ) // Skipped optional parameters
                            {
                                output.Append( v == String.Empty ? "," : ", " );
                            }

                            if (v == "default" && p != null)
                            {
                                v = $"default({p.GetFriendlyType()})";
                            }

                            if (p != null && (p.PropertyFlags & (ulong) Flags.PropertyFlagsLO.OutParm) != 0)
                            {
                                if(v.StartsWith("default("))
                                    v = $"/*null*/NullRef.{p.GetFriendlyType().Replace('<', '_').Replace('>', '_').Replace('.', '_')}";
                                v = "ref/*probably?*/ " + v; // UnrealScript's out is a pass by reference
                            }
                            if ((p as UByteProperty)?.EnumObject is UEnum enumObject)
                            {
                                if (int.TryParse(v, out var index))
                                {
                                    if (index < enumObject.Names.Count && index >= 0)
                                        v = $"{enumObject.GetFriendlyType()}.{enumObject.Names[index]}/*{v}*/";
                                    else
                                        v = $"/*val out of enum range*/{v}";
                                }
                                else
                                {
                                    v = $"({enumObject.GetFriendlyType()}){v}";
                                }
                            }

                            if (p is UByteProperty bProp && /*Not for out/refs params*/(bProp.PropertyFlags & (ulong) Flags.PropertyFlagsLO.OutParm) == 0 && bProp.EnumObject == null && int.TryParse(v, out _) == false)
                            {
                                v = $"(byte){v}"; // Casting enum to byte most likely
                            }

                            ParamsHack.Add(v);
                            output.Append( v );
                        }
                    }
                    return output.ToString();
                }
            }

            public class FinalFunctionToken : FunctionToken
            {
                public UFunction Function;

                public override void Deserialize( IUnrealStream stream )
                {
                    if( stream.Version == 421 )
                    {
                        Decompiler.AlignSize( sizeof(int) );
                    }

                    Function = stream.ReadObject() as UFunction;
                    Decompiler.AlignObjectSize();

                    DeserializeCall();
                }

                public override string Decompile()
                {
                    string output = String.Empty;
                    if( Function != null )
                    {
                        // Support for non native operators.
                        if( Function.IsPost() )
                        {
                            output = DecompilePreOperator( Function.Name );
                        }
                        else if( Function.IsPre() )
                        {
                            output = DecompilePostOperator( Function.Name );
                        }
                        else if( Function.IsOperator() )
                        {
                            output = DecompileOperator( Function.Name );
                        }
                        else
                        {
                            // Calling Super??.
                            if( Function.Name == Decompiler._Container.Name && Decompiler._IsWithinClassContext == ClassContext.No )
                            {
                                output = "base";

                                // Check if the super call is within the super class of this functions outer(class)
                                var myouter = (UField)Decompiler._Container.Outer;
                                if( myouter == null || myouter.Super == null || Function.GetOuterName() != myouter.Super.Name  )
                                {
                                    // There's no super to call then do a recursive super call.
                                    if( Decompiler._Container.Super == null )
                                    {
                                        output += "(" + Decompiler._Container.GetOuterName() + ")";
                                    }
                                    else
                                    {
                                        // Different owners, then it is a deep super call.
                                        if( Function.GetOuterName() != Decompiler._Container.GetOuterName() )
                                        {
                                            if(Decompiler._Container.Super.GetOuterName() != Function.GetOuterName())
                                                output += "(" + Function.GetOuterName() + ")"; // C# doesn't handle calling an indirect base function 
                                        }
                                    }
                                }
                                output += ".";
                                
                                // Workaround for UScript's inheritance and overriding system specificities that C# can't handle 
                                if (Function.Outer.GetType() == typeof(UState) || Function.OverridenByState)
                                {
                                    // Find closest (defined) base function. 
                                    var f = Function;
                                    while (f?.HasFunctionFlag(Flags.FunctionFlags.Defined) == false)
                                        f = f.Super as UFunction;
                                    
                                    if (f == null)
                                    {
                                        // When no defined functions were found pick the first declaration, that can be the case for 'Touch' on 'Actor' for example
                                        // That function is not defined but still is declared as it is called by the engine and can be overriden by states and inheritors
                                        f = Function;
                                        while (f!.Super != null)
                                            f = f.Super as UFunction;
                                    }
                                    
                                    output = $"/*Transformed '{output}' to specific call*/{f.NameOfSpecificFunctionImplementation}";
                                    output = output.Remove(output.Length - Function.Name.Length);
                                }
                            }
                            if (Function.HasFunctionFlag(Flags.FunctionFlags.Latent))
                                output += "yield return ";
                            
                            output += DecompileCall( Function.Name );
                        }
                    }
                    Decompiler._CanAddSemicolon = true;
                    return output;
                }

                public override string FunctionName => Function.Name;
            }

            public class VirtualFunctionToken : FunctionToken
            {
                public int FunctionNameIndex;

                public override void Deserialize( IUnrealStream stream )
                {
                    // TODO: Corrigate Version (Definitely not in MOHA, but in roboblitz(369))
                    if( stream.Version >= 178 && stream.Version < 421/*MOHA*/ )
                    {
                        byte isSuperCall = stream.ReadByte();
                        Decompiler.AlignSize( sizeof(byte) );
                    }

                    if( stream.Version == 421 )
                    {
                        Decompiler.AlignSize( sizeof(int) );
                    }

                    FunctionNameIndex = stream.ReadNameIndex();
                    Decompiler.AlignNameSize();

                    DeserializeCall();
                }

                public override string Decompile()
                {
                    Decompiler._CanAddSemicolon = true;
                    return DecompileCall( Package.GetIndexName( FunctionNameIndex ) );
                }

                public override string FunctionName => Package.GetIndexName(FunctionNameIndex);
            }

            public class GlobalFunctionToken : FunctionToken
            {
                public int FunctionNameIndex;

                public override void Deserialize( IUnrealStream stream )
                {
                    FunctionNameIndex = stream.ReadNameIndex();
                    Decompiler.AlignNameSize();

                    DeserializeCall();
                }

                public override string Decompile()
                {
                    Decompiler._CanAddSemicolon = true;
                    return "global_" + DecompileCall( Package.GetIndexName( FunctionNameIndex ) );
                }

                public override string FunctionName => Package.GetIndexName(FunctionNameIndex);
            }

            public class DelegateFunctionToken : FunctionToken
            {
                public int FunctionNameIndex;

                public override void Deserialize( IUnrealStream stream )
                {
                    // TODO: Corrigate Version
                    if( stream.Version > 180 )
                    {
                        ++ stream.Position; // ReadByte()
                        Decompiler.AlignSize( sizeof(byte) );
                    }

                    // Delegate object index
                    stream.ReadObjectIndex();
                    Decompiler.AlignObjectSize();

                    // Delegate name index
                    FunctionNameIndex = stream.ReadNameIndex();
                    Decompiler.AlignNameSize();

                    DeserializeCall();
                }

                public override string Decompile()
                {
                    Decompiler._CanAddSemicolon = true;
                    return DecompileCall( Decompiler._Container.Package.GetIndexName( FunctionNameIndex ) );
                }

                public override string FunctionName => Decompiler._Container.Package.GetIndexName( FunctionNameIndex );
            }

            public class NativeFunctionToken : FunctionToken
            {
                public NativeTableItem NativeTable;

                public override void Deserialize( IUnrealStream stream )
                {
                    if( NativeTable == null )
                    {
                        NativeTable = new NativeTableItem
                        {
                            Type = FunctionType.Function,
                            Name = "UnresolvedNativeFunction_" + RepresentToken,
                            ByteToken = RepresentToken
                        };
                    }

                    switch( NativeTable.Type )
                    {
                        case FunctionType.Function:
                            DeserializeCall();
                            break;

                        case FunctionType.PreOperator:
                        case FunctionType.PostOperator:
                            DeserializeUnaryOperator();
                            break;

                        case FunctionType.Operator:
                            DeserializeBinaryOperator();
                            break;

                        default:
                            DeserializeCall();
                            break;
                    }
                }

                public override string Decompile()
                {
                    // Try to find inherited function
                    UFunction foundFunc = null;
                    for (var container = Decompiler._Container as UState; container != null && foundFunc == null; container = container.Super as UState)
                    {
                        foreach (UFunction function in container.Functions)
                        {
                            if (function.Name != NativeTable.Name)
                                continue;
                                            
                            foundFunc = function;
                            break;
                        }

                        if (foundFunc != null || container.GetType() != typeof(UState))
                            continue;
                                
                        for (var Class = container.Outer as UClass; Class != null && foundFunc == null; Class = Class.Super as UClass)
                        {
                            foreach (UFunction function in Class.Functions)
                            {
                                if (function.Name != NativeTable.Name)
                                    continue;
                                        
                                foundFunc = function;
                                break;
                            }
                        }
                    }
                    
                    string output;
                    switch( NativeTable.Type )
                    {
                        case FunctionType.Operator:
                            output = DecompileOperator( NativeTable.Name );
                            break;

                        case FunctionType.PostOperator:
                            output = DecompilePostOperator( NativeTable.Name );
                            break;

                        case FunctionType.PreOperator:
                            output = DecompilePreOperator( NativeTable.Name );
                            break;

                        case FunctionType.Function:
                        default:
                            output = DecompileCall( NativeTable.Name );
                            break;
                    }
                    
                    if (foundFunc?.HasFunctionFlag(Flags.FunctionFlags.Latent) == true)
                        output = "yield return " + output;
                    
                    Decompiler._CanAddSemicolon = true;
                    return output;
                }

                public override string FunctionName => NativeTable.Name;
            }
        }
    }
}