#if DECOMPILE
using System;
using System.Linq;

namespace UELib.Core
{
    public partial class UStruct
    {
        /// <summary>
        /// Decompiles this object into a text format of:
        ///
        /// struct [FLAGS] NAME [extends NAME]
        /// {
        ///     [STRUCTCPPTEXT]
        ///
        ///     [CONSTS]
        ///
        ///     [ENUMS]
        ///
        ///     [STRUCTS]
        ///
        ///     [VARIABLES]
        ///
        ///     [STRUCTDEFAULTPROPERTIES]
        /// };
        /// </summary>
        /// <returns></returns>
        public override string Decompile()
        {
            string content = UDecompilingState.Tabs + FormatHeader() +
                UnrealConfig.PrintBeginBracket();
            UDecompilingState.AddTabs( 1 );
            string cpptext = FormatCPPText();
            string props = FormatProperties();

            string defProps = FormatDefaultProperties();
            if( defProps.Length != 0 )
            {
                defProps += "\r\n";
            }
            UDecompilingState.RemoveTabs( 1 );
            content += cpptext + props + defProps;
            if( content.EndsWith( "\r\n" ) )
            {
                content = content.TrimEnd( '\r', '\n' );
            }
            return content + UnrealConfig.PrintEndBracket() + ";";
        }

        protected override string FormatHeader()
        {
            var flags = FormatFlags();
            if (string.IsNullOrWhiteSpace(flags) == false)
                flags = $"/*{flags}*/";

            // Note: C# doesn't support struct-struct inheritance, commenting out extends part, inheritance is replaced by copy pasting parent content into it
            var output = $"public partial struct {flags}{Name}" + (Super != null ? $"// {FormatExtends()} {Super.Name}" : String.Empty); 
            var metaData = DecompileMeta();
            if( metaData != String.Empty )
            {
                output = metaData + "\r\n" + UDecompilingState.Tabs + output;
            }
            return output;
        }

        private string FormatFlags()
        {
            string output = String.Empty;
            if( StructFlags == 0 )
            {
                return String.Empty;
            }

            if( (StructFlags & (uint)Flags.StructFlags.Native) != 0 )
            {
                output += "native ";
            }

            if( (StructFlags & (uint)Flags.StructFlags.Export) != 0 )
            {
                output += "export ";
            }

            if( Package.Version <= 128 )
            {
                if( (StructFlags & (uint)Flags.StructFlags.Long) != 0 )
                {
                    output += "long ";
                }
            }

            if( (StructFlags & (uint)Flags.StructFlags.Init) != 0 && Package.Version < 222 )
            {
                output += "init ";
            }
            else if( HasStructFlag( Flags.StructFlags.Transient ) )
            {
                output += "transient ";
            }

            if( HasStructFlag( Flags.StructFlags.Atomic ) )
            {
                output += "atomic ";
            }

            if( HasStructFlag( Flags.StructFlags.AtomicWhenCooked ) )
            {
                output += "atomicwhencooked ";
            }

            if( HasStructFlag( Flags.StructFlags.Immutable ) )
            {
                output += "immutable ";
            }

            if( HasStructFlag( Flags.StructFlags.ImmutableWhenCooked ) )
            {
                output += "immutablewhencooked ";
            }

            if( HasStructFlag( Flags.StructFlags.StrictConfig ) )
            {
                output += "strictconfig ";
            }
            return output;
        }

        protected virtual string CPPTextKeyword
        {
            get{ return Package.Version < VCppText ? "cppstruct" : "structcpptext"; }
        }

        protected string FormatCPPText()
        {
            if( CppText == null )
            {
                return String.Empty;
            }

            string output = String.Format( "\r\n{0}{1}{2}\r\n",
                UDecompilingState.Tabs,
                CPPTextKeyword,
                UnrealConfig.PrintBeginBracket()
            );
            output += CppText.Decompile() + UnrealConfig.PrintEndBracket() + "\r\n";
            return output;
        }

        protected string FormatConstants()
        {
            if( Constants == null || !Constants.Any() )
                return String.Empty;

            string output = String.Empty;
            foreach( var scriptConstant in Constants )
            {
                try
                {
                    output += "\r\n" + UDecompilingState.Tabs + scriptConstant.Decompile();
                }
                catch
                {
                    output += String.Format( "\r\nFailed at decompiling const: {0}", scriptConstant.Name );
                }
            }
            return output + "\r\n";
        }

        protected string FormatEnums()
        {
            if( Enums == null || !Enums.Any() )
                return String.Empty;

            string output = String.Empty;
            foreach( var scriptEnum in Enums )
            {
                try
                {
                    // And add a empty line between all enums!
                    output += "\r\n" + scriptEnum.Decompile() + "\r\n";
                }
                catch
                {
                    output += String.Format( "\r\nFailed at decompiling enum: {0}", scriptEnum.Name );
                }
            }
            return output;
        }

        protected string FormatStructs()
        {
            if( Structs == null || !Structs.Any() )
                return String.Empty;

            string output = String.Empty;
            foreach( var scriptStruct in Structs )
            {
                // And add a empty line between all structs!
                try
                {
                    output += "\r\n" + scriptStruct.Decompile() + "\r\n";
                }
                catch(Exception e)
                {
                    output += e.OutputWarningException( $"Failed at decompiling struct: {scriptStruct.Name}" );
                }
            }
            return output;
        }

        protected string FormatProperties()
        {
            // Note: C# doesn't support struct-struct inheritance, pasting content of super in here
            string output = IsClassType( "Class" ) ? "" : (Super as UStruct)?.FormatProperties() ?? String.Empty;
            if (Variables == null || !Variables.Any())
                return output;

            // Only for pure UStructs because UClass handles this on its own
            if( IsPureStruct() )
            {
                output += FormatConstants() + FormatEnums() + FormatStructs();
            }

            // Don't use foreach, screws up order.
            foreach( var property in Variables )
            {
                try
                {
                    // Fix for properties within structs
                    output += "\r\n" + property.PreDecompile() + UDecompilingState.Tabs + "public";
                    try
                    {
                        if( property.CategoryIndex > -1
                            && String.Compare( property.CategoryName, "None",
                                               StringComparison.OrdinalIgnoreCase ) != 0 )
                        {
                            if( property.CategoryName != Name )
                            {
                                output += "/*(" + property.CategoryName + ")*/";
                            }
                            else
                            {
                                output += "/*()*/";
                            }
                        }
                    }
                    catch( ArgumentOutOfRangeException )
                    {
                        output += String.Format( "/* INDEX:{0} */", property.CategoryIndex );
                    }

                    output += " " + property.Decompile();
                    
                    if( this is UClass c2 && c2.IsClassInterface() )
                    {
                        output += "{ get; }";
                    }
                    else if( this is UClass c 
                             && c.ImplementedInterfaces != null
                             && (from i in c.ImplementedInterfaces 
                                 where (Package.GetIndexTable( i ).Object as UClass)?.Variables != null
                                 from v in (Package.GetIndexTable( i ).Object as UClass).Variables 
                                 where v.Name == property.Name 
                                 select v).FirstOrDefault() is UProperty p )
                    {
                        output += "{ get; set; }";
                    }
                    else
                    {
                        output += ";";
                    }
                }
                catch( Exception e )
                {
                    output += e.OutputWarningException( $"Property:{property.Name} threw the following exception:" );
                }
            }
            return output + "\r\n";
        }

        public string FormatDefaultProperties()
        {
            string output = String.Empty;
            // Note: C# doesn't support struct-struct inheritance, pasting content of super in here
            using(UDecompilingState.TabScope())
                output += IsClassType( "Class" ) ? "" : (Super as UStruct)?.DecompileProperties();
            
            if( Default != null && Default != this )
            {
                Default.BeginDeserializing();
            }

            if( string.IsNullOrEmpty(output) && (Properties == null || !Properties.Any()) )
                return output;

            string innerOutput;

            if( IsClassType( "Class" ) )
            {
                output += $"\r\npublic {Name}()\r\n{{\r\n";
            }
            else
            {
                output += "\r\n" + UDecompilingState.Tabs + "structdefaultproperties\r\n"
                          + UDecompilingState.Tabs + "{\r\n";
            }

            UDecompilingState.AddTabs( 1 );
            try
            {
                innerOutput = DecompileProperties();
            }
            catch( Exception e )
            {
                innerOutput = e.OutputWarningException( $"// {e.GetType().Name} occurred while decompiling properties!" );
            }
            finally
            {
                UDecompilingState.RemoveTabs( 1 );
            }

            output += innerOutput + UDecompilingState.Tabs + "}";

            if (IsClassType("Class") == false)
            {
                output = output.Replace("\n", "\n//");
            }

            return output;
        }

        protected string FormatLocals()
        {
            if( Locals == null || !Locals.Any() )
                return String.Empty;

            int numParms = 0;
            string output = String.Empty;
            string lastType = String.Empty;
            for( var i = 0; i < Locals.Count; ++ i )
            {
                string curType = Locals[i].GetFriendlyType();
                string nextType = ((i + 1) < Locals.Count
                    ? Locals[i + 1].GetFriendlyType()
                    : String.Empty);

                // If previous is the same as the one now then format the params as one line until another type is reached
                if( curType == lastType )
                {
                    output += Locals[i].Name +
                    (
                        curType == nextType
                        ? ((numParms >= 5 && numParms % 5 == 0)
                            ? " = default,\r\n\t" + UDecompilingState.Tabs
                            : " = default, "
                          )
                        : " = default;\r\n"
                    );
                    ++ numParms;
                }
                else
                {
                    output += (numParms >= 5 ? "\r\n" : String.Empty)
                        + UDecompilingState.Tabs + "/*local */" + Locals[i].Decompile() +
                    (
                        (nextType != curType || String.IsNullOrEmpty( nextType ) )
                        ? " = default;\r\n"
                        : " = default, "
                    );
                    numParms = 1;
                }
                lastType = curType;
            }
            return output;
        }

        protected string DecompileScript()
        {
            return ByteCodeManager != null ? ByteCodeManager.Decompile() : String.Empty;
        }
    }
}
#endif