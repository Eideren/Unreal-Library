using System;

namespace UELib.Core
{
    public partial class UStruct
    {
        public partial class UByteCodeDecompiler
        {
            public class LetToken : Token
            {
                public override void Deserialize( IUnrealStream stream )
                {
                    // A = B
                    DeserializeNext();
                    DeserializeNext();
                }

                public override string Decompile()
                {
                    Decompiler._CanAddSemicolon = true;
                    
                    Token t;
                    while ((t = Decompiler.MoveToNextToken()) is DebugInfoToken){}

                    if (Decompiler.PreviousToken is SwitchToken sw)
                        sw.ParamHack = t;

                    string firstTokenResult;
                    try
                    {
                        firstTokenResult = t.Decompile();
                    }
                    catch( Exception e )
                    {
                        firstTokenResult = t.GetType().Name + "(" + e.GetType().Name + ")";
                    }

                    while (t is ContextToken ctx)
                        t = ctx.TargetHack;

                    var decompiledNext = DecompileNext();
                    if (t is FieldToken ft && ft.Object is UByteProperty prop)
                    {
                        if (prop.EnumObject != null)
                        {
                            if (int.TryParse(decompiledNext, out var index))
                            {
                                var enumObject = prop.EnumObject;
                                if (index < enumObject.Names.Count)
                                    decompiledNext = $"{enumObject.GetFriendlyType()}.{enumObject.Names[index]}/*{decompiledNext}*/";
                                else
                                    decompiledNext = $"/*val out of enum range*/{decompiledNext}";
                            }
                            else
                            {
                                decompiledNext = $"({prop.EnumObject.GetFriendlyType()}){decompiledNext}";
                            }
                        }
                        else
                        {
                            decompiledNext = "(byte)"+decompiledNext;
                        }
                    }
                    
                    return $"{firstTokenResult} = {decompiledNext}";
                }
            }

            public class LetBoolToken : LetToken
            {
            }

            public class LetDelegateToken : LetToken
            {
            }

            public class EndParmValueToken : Token
            {
                public override string Decompile()
                {
                    return String.Empty;
                }
            }

            public class ConditionalToken : Token
            {
                public override void Deserialize( IUnrealStream stream )
                {
                    // Condition
                    DeserializeNext();

                    // Size. Used to skip ? if Condition is False.
                    stream.ReadUInt16();
                    Decompiler.AlignSize( sizeof(ushort) );

                    // If TRUE expression
                    DeserializeNext();

                    // Size. Used to skip : if Condition is True.
                    stream.ReadUInt16();
                    Decompiler.AlignSize( sizeof(ushort) );

                    // If FALSE expression
                    DeserializeNext();
                }

                public override string Decompile()
                {
                    return "((" + DecompileNext() + ") ? " + DecompileNext() + " : " + DecompileNext() + ")";
                }
            }
        }
    }
}