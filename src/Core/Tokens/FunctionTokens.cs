using System;
using System.Collections.Generic;
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
                    if (operatorName == "$")
                        operatorName = "+"; // string concat
                    if (operatorName == "@")
                        operatorName = "+ \" \" +"; // spaced string concat
                    
                    if (operatorName == "Dot" || operatorName == "Cross")
                    {
                        output = $"{operatorName}({PrecedenceToken( GrabNextToken() )}, {PrecedenceToken( GrabNextToken() )})";
                    }
                    else
                    {
                        output = String.Format( "{0} {1} {2}",
                            PrecedenceToken( GrabNextToken() ),
                            operatorName,
                            PrecedenceToken( GrabNextToken() )
                        );
                    }

                    DecompileNext(); // )
                    return output;
                }

                protected string DecompilePostOperator( string operatorName )
                {
                    string output = operatorName + " " + DecompileNext();
                    DecompileNext(); // )
                    return output;
                }

                protected string DecompileCall( string functionName )
                {
                    if( Decompiler._IsWithinClassContext )
                    {
                        functionName = "static." + functionName;

                        // Set false elsewhere as well but to be sure we set it to false here to avoid getting static calls inside the params.
                        // e.g.
                        // A1233343.DrawText(Class'BTClient_Interaction'.static.A1233332(static.Max(0, A1233328 - A1233322[A1233222].StartTime)), true);
                        Decompiler._IsWithinClassContext = false;
                    }
                    string output = functionName + "(" + DecompileParms(functionName);
                    return output;
                }

                private string DecompileParms(string functionName)
                {
                    var tokens = new List<(Token token, String output, UProperty prop)>();
                    {
                    next:
                        var t = GrabNextToken();
                        tokens.Add( ( t, t.Decompile(), null ) );
                        if( !(t is EndFunctionParmsToken) )
                            goto next;
                    }
                    
                    // Try to find matching function in the hierarchy to fix ref stuff
                    var outer = ((Decompiler._Container as UFunction)?.Outer as UState) ?? (Decompiler._Container as UState);
                    foreach (var function in outer.InheritedFunctions())
                    {
                        if (function.Name != functionName)
                            continue;
                            
                        var paramCount = function.Params.Count - (function.ReturnProperty == null ? 0 : 1);
                        if (paramCount != (tokens.Count - 1/* - EndFunction*/))
                            continue;

                        int index = 0;
                        foreach (var param in function.Params)
                        {
                            if (param == function.ReturnProperty)
                                continue;
                            var p = tokens[index];
                            tokens[index] = (p.token, p.output, param);
                            index++;
                        }
                        break;
                    }

                    var output = new StringBuilder();
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
                            if( t is OutVariableToken )
                                output.Append( "ref " );
                            else if(  (p != null && (p.PropertyFlags & (ulong) Flags.PropertyFlagsLO.OutParm) != 0) )
                                output.Append( "ref/*probably?*/ " );
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
                            if( Function.Name == Decompiler._Container.Name && !Decompiler._IsWithinClassContext )
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
                                if (Function.Outer.GetType() == typeof(UState) || Function.OverridenByState)
                                {
                                    output = "/*Call to base transformed into specific call because of state overriding that function */" + Function.NameOfSpecificFunctionImplementation;
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
            }
        }
    }
}