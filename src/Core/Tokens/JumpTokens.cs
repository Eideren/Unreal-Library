using System;
using System.Collections.Generic;
using System.Linq;

namespace UELib.Core
{
    public partial class UStruct
    {
        public partial class UByteCodeDecompiler
        {
            public class ReturnToken : Token
            {
                public override void Deserialize( IUnrealStream stream )
                {
                    // Expression
                    DeserializeNext();
                }

                public override string Decompile()
                {
                    #region CaseToken Support
                    // HACK: for case's that end with a return instead of a break.
                    if( Decompiler.IsInNest( NestManager.Nest.NestType.Default ) != null )
                    {
                        Decompiler._Nester.AddNestEnd( NestManager.Nest.NestType.Switch, Position + Size );
                    }
                    #endregion

                    string returnValue = DecompileNext();
                    Decompiler._CanAddSemicolon = true;
                    return "return" + (returnValue.Length != 0 ? " " + returnValue : String.Empty);
                }
            }

            public class ReturnNothingToken : Token
            {
                private UObject _ReturnObject;

                public override void Deserialize( IUnrealStream stream )
                {
                    // TODO: Corrigate version.
                    if( stream.Version <= 300 )
                        return;

                    _ReturnObject = Decompiler._Container.TryGetIndexObject( stream.ReadObjectIndex() );
                    Decompiler.AlignObjectSize();
                }

                public override string Decompile()
                {
                    #region CaseToken Support
                    // HACK: for case's that end with a return instead of a break.
                    if( Decompiler.IsInNest( NestManager.Nest.NestType.Case ) != null )
                    {
                        Decompiler._Nester.AddNestEnd( NestManager.Nest.NestType.Case, Position + Size );
                    }
                    else if( Decompiler.IsInNest( NestManager.Nest.NestType.Default ) != null )
                    {
                        Decompiler._Nester.AddNestEnd( NestManager.Nest.NestType.Switch, Position + Size );
                    }
                    #endregion
                    return _ReturnObject != null ? _ReturnObject.Name : String.Empty;
                }
            }

            public class GoToLabelToken : Token
            {
                public override void Deserialize( IUnrealStream stream )
                {
                    // Expression
                    DeserializeNext();
                }

                public override string Decompile()
                {
                    Decompiler._CanAddSemicolon = true;
                    return "goto " + DecompileNext();
                }
            }

            public class JumpToken : Token
            {
                public ushort CodeOffset{ get; private set; }

                public override void Deserialize( IUnrealStream stream )
                {
                    CodeOffset = stream.ReadUInt16();
                    Decompiler.AlignSize( sizeof(ushort) );
                }

                public override void PostDeserialized()
                {
                    if( GetType() == typeof(JumpToken) )
                        Decompiler._Labels.Add
                        (
                            new ULabelEntry
                            {
                                Name = String.Format( "J0x{0:X2}", CodeOffset ),
                                Position = CodeOffset
                            }
                        );
                }

                protected void Commentize()
                {
                    Decompiler.PreComment = String.Format( "// End:0x{0:X2}", CodeOffset );
                }

                protected void Commentize( string statement )
                {
                    Decompiler.PreComment = String.Format( "// End:0x{0:X2} [{1}]", CodeOffset, statement );
                }

                protected void CommentStatement( string statement )
                {
                    Decompiler.PreComment = String.Format( "// [{0}]", statement );
                }

                /// <summary>
                /// FORMATION ISSUESSES:
                ///     1:(-> Logic remains the same)   Continue's are decompiled as Else's statements e.g.
                ///         -> Original
                ///         if( continueCondition )
                ///         {
                ///             continue;
                ///         }
                ///
                ///         // Actual code
                ///
                ///         -> Decompiled
                ///         if( continueCodition )
                ///         {
                ///         }
                ///         else
                ///         {
                ///             // Actual code
                ///         }
                ///
                ///
                ///     2:(-> ...)  ...
                ///         -> Original
                ///             ...
                ///         -> Decompiled
                ///             ...
                ///
                /// </summary>
                public override string Decompile()
                {
                    // Break offset!
                    if( CodeOffset >= Position )
                    {
                        if(
                            //==================We're inside a Case and at the end of it!
                            Decompiler.IsInNest( NestManager.Nest.NestType.Case ) != null
                            //==================We're inside a Default and at the end of it!
                            || Decompiler.IsInNest( NestManager.Nest.NestType.Default ) != null)
                        {
                            NoJumpLabel();
                            Commentize();
                            // 'break' CodeOffset sits at the end of the switch,
                            // check that it doesn't exist already and add it
                            var switchEnd = Decompiler.IsInNest( NestManager.Nest.NestType.Default ) != null ? Position + Size : CodeOffset;
                            if ((from n in Decompiler._Nester.Nests
                                where n.Type == NestManager.Nest.NestType.Switch && n.Position == switchEnd
                                select n).FirstOrDefault() == null)
                            {
                                Decompiler._Nester.AddNestEnd( NestManager.Nest.NestType.Switch, switchEnd );
                            }
                            Decompiler._CanAddSemicolon = true;
                            return "break";
                        }

                        var nest = Decompiler.IsWithinNest( NestManager.Nest.NestType.ForEach );
                        if( nest != null )
                        {
                            var dest = Decompiler.TokenAt( CodeOffset );
                            if( dest != null )
                            {
                                if( dest is IteratorPopToken )
                                {
                                    if( Decompiler.PreviousToken is IteratorNextToken )
                                    {
                                        NoJumpLabel();
                                        return String.Empty;
                                    }

                                    NoJumpLabel();
                                    Commentize();
                                    Decompiler._CanAddSemicolon = true;
                                    return "break";
                                }

                                if( dest is IteratorNextToken )
                                {
                                    NoJumpLabel();
                                    Commentize();
                                    Decompiler._CanAddSemicolon = true;
                                    return "continue";
                                }
                            }
                        }

                        nest = Decompiler.IsWithinNest( NestManager.Nest.NestType.Loop );
                        if( nest != null )
                        {
                            var dest = nest.Creator as JumpToken;
                            if( dest != null )
                            {
                                if( CodeOffset + 10 == dest.CodeOffset )
                                {
                                    CommentStatement( "Explicit Continue" );
                                    goto gotoJump;
                                }

                                if( CodeOffset == dest.CodeOffset )
                                {
                                    CommentStatement( "Explicit Break" );
                                    goto gotoJump;
                                }
                            }
                        }

                        nest = Decompiler.IsInNest( NestManager.Nest.NestType.If );
                        //==================We're inside a If and at the end of it!
                        if( nest != null )
                        {
                            // We're inside a If however the kind of jump could range from: continue; break; goto; else
                            var nestEnd = Decompiler.CurNestEnd();
                            if( nestEnd != null )
                            {
                                // Else, Req:In If nest, nestends > 0, curendnest position == Position
                                if( nestEnd.Position - Size == Position )
                                {
                                    // HACK: This should be handled by UByteCodeDecompiler.Decompile()
                                    UDecompilingState.RemoveTabs( 1 );
                                    Decompiler._NestChain.RemoveAt( Decompiler._NestChain.Count - 1 );
                                    Decompiler._Nester.Nests.Remove( nestEnd );

                                    Decompiler._Nester.AddNest( NestManager.Nest.NestType.Else, Position, CodeOffset );

                                    NoJumpLabel();

                                    // HACK: This should be handled by UByteCodeDecompiler.Decompile()
                                    return "}" + "\r\n" +
                                        (UnrealConfig.SuppressComments
                                        ? UDecompilingState.Tabs + "else"
                                        : UDecompilingState.Tabs + String.Format( "// End:0x{0:X2}", CodeOffset ) + "\r\n" +
                                            UDecompilingState.Tabs + "else");
                                }
                            }
                        }
                    }

                    if( CodeOffset < Position )
                    {
                        CommentStatement( "Loop Continue" );
                    }
                    gotoJump:
                    // This is an implicit GoToToken.
                    Decompiler._CanAddSemicolon = true;
                    return String.Format( "goto J0x{0:X2}", CodeOffset );
                }

                private void NoJumpLabel()
                {
                    int i = Decompiler._TempLabels.FindIndex( p => p.entry.Position == CodeOffset );
                    if( i != -1 )
                    {
                        var data = Decompiler._TempLabels[i];
                        if (data.refs == 1)
                        {
                            Decompiler._TempLabels.RemoveAt( i );
                        }
                        else
                        {
                            data.refs -= 1;
                            Decompiler._TempLabels[i] = data;
                        }
                    }
                }
            }

            public class JumpIfNotToken : JumpToken
            {
                public bool IsLoop;
                
                public override void PostDeserialized()
                {
                    base.PostDeserialized();
                    // Add jump label for 'do until' jump pattern
                    if ((CodeOffset & UInt16.MaxValue) < Position)
                    {
                        Decompiler._Labels.Add
                        (
                            new ULabelEntry
                            {
                                Name = $"J0x{CodeOffset}",
                                Position = CodeOffset
                            }
                        );
                    }
                }

                public override void Deserialize( IUnrealStream stream )
                {
                    // CodeOffset
                    base.Deserialize( stream );

                    // Condition
                    DeserializeNext();
                }

                public override string Decompile()
                {
                    // Check whether there's a JumpToken pointing to the begin of this JumpIfNot,
                    //  if detected, we assume that this If is part of a loop.
                    IsLoop = false;
                    for( int i = Decompiler.CurrentTokenIndex + 1; i < Decompiler.DeserializedTokens.Count; ++ i )
                    {
                        if( Decompiler.DeserializedTokens[i] is JumpToken && ((JumpToken)Decompiler.DeserializedTokens[i]).CodeOffset == Position )
                        {
                            IsLoop = true;
                            break;
                        }
                    }

                    Commentize();
                    if( IsLoop )
                    {
                        Decompiler.PreComment += " [Loop If]";
                    }

                    string condition = DecompileNext();
                    string output = /*(IsLoop ? "while" : "if") +*/ "if(" + condition + ")";

                    if( (CodeOffset & UInt16.MaxValue) < Position )
                    {
                        Decompiler.PreComment += " [Loop Until]";
                        Decompiler._CanAddSemicolon = true;
                        // Inverse condition only here as we're explicitly jumping while other cases create proper scopes 
                        return $"if(({condition}) == false)\r\n{UDecompilingState.Tabs}{UnrealConfig.Indention}goto J0x{CodeOffset:X2}";
                    }

                    Decompiler._CanAddSemicolon = false;
                    Decompiler._Nester.AddNest( IsLoop
                        ? NestManager.Nest.NestType.Loop
                        : NestManager.Nest.NestType.If,
                        Position, CodeOffset, this
                    );
                    return output;
                }
            }

            public class FilterEditorOnlyToken : JumpToken
            {
                public override string Decompile()
                {
                    Decompiler._Nester.AddNest( NestManager.Nest.NestType.Scope, Position, CodeOffset );
                    Commentize();
                    return "filtereditoronly";
                }
            }

            public class SwitchToken : Token
            {
                public ushort PropertyType;

                public override void Deserialize( IUnrealStream stream )
                {
#if TRANSFORMERS
                    if( Package.Build == UnrealPackage.GameBuild.BuildName.Transformers )
                    {
                        PropertyType = stream.ReadUInt16();
                        Decompiler.AlignSize( sizeof(ushort) );
                        goto deserialize;
                    }
#endif
                    if( stream.Version >= 600 )
                    {
                        // Points to the object that was passed to the switch,
                        // beware that the followed token chain contains it as well!
                        stream.ReadObjectIndex();
                        Decompiler.AlignObjectSize();
                    }

                    // TODO: Corrigate version
                    if( stream.Version >= 536 && stream.Version <= 587 )
                    {
                        PropertyType = stream.ReadUInt16();
                        Decompiler.AlignSize( sizeof(ushort) );
                    }
                    else
                    {
                        PropertyType = stream.ReadByte();
                        Decompiler.AlignSize( sizeof(byte) );
                    }

                deserialize:
                    // Expression
                    DeserializeNext();
                }

                /// <summary>
                /// FORMATION ISSUESSES:
                ///     1:(-> ...)  NestEnd is based upon the break in a default case, however a case does not always break/return,
                ///         causing that there will be no default with a break detected, thus no ending for the Switch block.
                ///
                ///         -> Original
                ///             Switch( A )
                ///             {
                ///                 case 0:
                ///                     CallA();
                ///             }
                ///
                ///             CallB();
                ///
                ///         -> Decompiled
                ///             Switch( A )
                ///             {
                ///                 case 0:
                ///                     CallA();
                ///                 default:    // End is detect of case 0 due some other hack :)
                ///                     CallB();
                /// </summary>
                public override string Decompile()
                {
                    Decompiler._Nester.AddNestBegin( NestManager.Nest.NestType.Switch, Position );

                    var t = GrabNextToken();
                    ParamHack = t;
                    string expr = t.DecompileAndCatch();
                    Decompiler._CanAddSemicolon = false;    // In case the decompiled token was a function call
                    return "switch(" + expr + ")";
                }

                public Token ParamHack;
            }

            public class CaseToken : JumpToken
            {
                public SwitchToken OwnerHack;
                public override void Deserialize( IUnrealStream stream )
                {
                    base.Deserialize( stream );
                    if( CodeOffset != UInt16.MaxValue )
                    {
                        DeserializeNext();  // Condition
                    }   // Else "Default:"
                }
                
                public override string Decompile()
                {
                    Commentize();
                    OwnerHack = Decompiler._NestChain[Decompiler._NestChain.Count - 1].Creator as SwitchToken;


                    bool isDefault = CodeOffset == UInt16.MaxValue;
                    if(isDefault)
                        Decompiler._Nester.AddNestBegin( NestManager.Nest.NestType.Default, Position, this );
                    else
                        Decompiler._Nester.AddNest( NestManager.Nest.NestType.Case, Position, CodeOffset );
                    string output = isDefault ? "default:" : $"case {DecompileNext()}:";
                    
                    
                    var i = Decompiler.DeserializedTokens.IndexOf(this);
                    var previous = Decompiler.DeserializedTokens[i - 1];

                    // Find previous case token and add an explicit fallthrough as C# doesn't support implicit fallthrough from non-empty cases 
                    if ((previous is NothingToken/*return nothing*/ || previous is JumpToken) == false)
                    {
                        for (int j = i-1; j > 0; j--)
                        {
                            if (Decompiler.DeserializedTokens[j] is CaseToken ct && ct.CodeOffset == Position && ReferenceEquals(ct.OwnerHack, OwnerHack))
                            {
                                if(ct.Position + ct.Size != Position) // the previous case is not an empty fallthrough
                                    output = $"\tgoto {output.Replace(':', ';')}// UnrealScript fallthrough\r\n{UDecompilingState.Tabs}{output}";
                            }
                        }
                    }

                    Decompiler._CanAddSemicolon = false;
                    return output;
                }
            }

            public class IteratorToken : JumpToken
            {
                public override void Deserialize( IUnrealStream stream )
                {
                    DeserializeNext();  // Expression
                    base.Deserialize( stream );
                }

                public override string Decompile()
                {
                    Decompiler._Nester.AddNest( NestManager.Nest.NestType.ForEach, Position, CodeOffset, this );
                    Commentize();
                    
                    var t = GrabNextToken();
                    string expression = t.DecompileAndCatch();
                    var asFunction = t as FunctionToken;
                    if (asFunction == null && t is ContextToken ctx && ctx.TargetHack is FunctionToken fToken)
                        asFunction = fToken;
                    if (asFunction == null)
                        System.Diagnostics.Debugger.Break();
                    
                    // foreach FunctionCall
                    Decompiler._CanAddSemicolon = false;    // Undo
                    
                    var iteratorVariable = "";
                    var funcParam = "";
                    foreach (var parameter in asFunction.ParamsHack)
                    {
                        if (parameter.StartsWith("ref "))
                            iteratorVariable += parameter.Remove(0, "ref ".Length);
                        else if(parameter.StartsWith("ref/*probably?*/ "))
                            iteratorVariable += parameter.Remove(0, "ref/*probably?*/ ".Length);
                        else
                            funcParam += parameter + ", ";
                    }

                    if (funcParam != "")
                        funcParam = funcParam.Substring(0, funcParam.Length - 2);
                    
                    var additionalCast = "";
                    if (iteratorVariable == "")
                        iteratorVariable = "/*no refs specified*/";
                    else if (Decompiler._Container is UFunction f && (from x in f.Locals where x.Name == iteratorVariable select x).FirstOrDefault() is UProperty matchingLocal)
                        additionalCast = $"({matchingLocal.GetFriendlyType()})";
                    else if(iteratorVariable.StartsWith("NullRef.") == false)
                        System.Diagnostics.Debugger.Break();

                    if (funcParam.StartsWith("ClassT<") && funcParam.EndsWith(">()"))
                        additionalCast = $"({funcParam.Substring(0, funcParam.Length - ">()".Length).Remove(0, "ClassT<".Length)})";


                    var enumName = $"e{Position}";
                    var funcDef = expression.Substring(0, expression.IndexOf(asFunction.FunctionName) + asFunction.FunctionName.Length);
                    return $"\r\n{UDecompilingState.Tabs}// foreach {expression}\r\n{UDecompilingState.Tabs}using var {enumName} = {funcDef}({funcParam}).GetEnumerator();\r\n{UDecompilingState.Tabs}while({enumName}.MoveNext() && ({iteratorVariable} = {additionalCast}{enumName}.Current) == {iteratorVariable})";
                }
            }

            public class ArrayIteratorToken : JumpToken
            {
                protected bool HasSecondParm;

                public override void Deserialize( IUnrealStream stream )
                {
                    // Expression
                    DeserializeNext();

                    // Param 1
                    DeserializeNext();

                    HasSecondParm = stream.ReadByte() > 0;
                    Decompiler.AlignSize( sizeof(byte) );
                    DeserializeNext();

                    base.Deserialize( stream );
                }

                public override string Decompile()
                {
                    Decompiler._Nester.AddNest( NestManager.Nest.NestType.ForEach, Position, CodeOffset, this );

                    Commentize();

                    // foreach ArrayVariable( Parameters )
                    var iterator = DecompileNext();
                    var varName = DecompileNext();
                    var param = DecompileNext();
                    string output = "foreach(var " + varName + " in " + iterator;

                    string additionalCast = null;
                    if (Decompiler._Container is UFunction f && (from x in f.Locals where x.Name == varName select x).FirstOrDefault() is UProperty matchingLocal)
                        additionalCast = $"({matchingLocal.GetFriendlyType()})";
                    
                    
                    if (HasSecondParm)
                        output += (HasSecondParm ? ", " : String.Empty) + param;
                    else
                        output = $"using var v = {iterator}.GetEnumerator();while(v.MoveNext() && ({varName} = {additionalCast}v.Current) == {varName}";
                    Decompiler._CanAddSemicolon = false;
                    return output + ")";
                }
            }

            public class IteratorNextToken : Token
            {
                public override string Decompile()
                {
                    if( Decompiler.PeekToken is IteratorPopToken )
                    {
                        return String.Empty;
                    }
                    Decompiler._CanAddSemicolon = true;
                    return "continue";
                }
            }

            public class IteratorPopToken : Token
            {
                public override string Decompile()
                {
                    if( Decompiler.PreviousToken is IteratorNextToken
                        || Decompiler.PeekToken is ReturnToken )
                    {
                        return String.Empty;
                    }
                    Decompiler._CanAddSemicolon = true;
                    return "break";
                }
            }

            private List<ULabelEntry> _Labels;
            private List<(ULabelEntry entry, int refs)> _TempLabels;
            public class LabelTableToken : Token
            {
                public override void Deserialize( IUnrealStream stream )
                {
                    var label = String.Empty;
                    int labelPos = -1;
                    do
                    {
                        if( label != String.Empty )
                        {
                            Decompiler._Labels.Add
                            (
                                new ULabelEntry
                                {
                                    Name = label,
                                    Position = labelPos
                                }
                            );
                        }
                        label = stream.ReadName();
                        Decompiler.AlignNameSize();
                        labelPos = stream.ReadInt32();
                        Decompiler.AlignSize( sizeof(int) );
                    } while( String.Compare( label, "None", StringComparison.OrdinalIgnoreCase ) != 0 );
                }
            }

            public class SkipToken : Token
            {
                public override void Deserialize( IUnrealStream stream )
                {
                    stream.ReadUInt16();    // Size
                    Decompiler.AlignSize( sizeof(ushort) );

                    DeserializeNext();
                }

                public override string Decompile()
                {
                     return DecompileNext();
                }
            }

            public class StopToken : Token
            {
                public override string Decompile()
                {
                    Decompiler._CanAddSemicolon = true;
                    return "yield return Flow.Stop";
                }
            }
        }
    }
}