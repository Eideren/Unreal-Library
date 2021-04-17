#if DECOMPILE
using System;
using System.Linq;

namespace UELib.Core
{
    public partial class UFunction
    {
        /// <summary>
        /// Decompiles this object into a text format of:
        ///
        /// [FLAGS] function NAME([VARIABLES]) [const]
        /// {
        ///     [LOCALS]
        ///
        ///     [CODE]
        /// }
        /// </summary>
        /// <returns></returns>
        public override string Decompile()
        {
            string code;
            try
            {
                code = FormatCode();
            }
            catch( Exception e )
            {
                code = e.Message;
            }
            return FormatHeader() + (String.IsNullOrEmpty( code ) ? ";" : code);
        }

        private string FormatFlags()
        {
            string output = String.Empty;
            string importantOutput = String.Empty;
            bool isNormalFunction = true;

            if( HasFunctionFlag( Flags.FunctionFlags.Private ) )
            {
                output += "private ";
            }
            else if( HasFunctionFlag( Flags.FunctionFlags.Protected ) )
            {
                output += "protected ";
            }

            if( Package.Version >= UnrealPackage.VDLLBIND && HasFunctionFlag( Flags.FunctionFlags.DLLImport ) )
            {
                output += "dllimport ";
            }

            if( Package.Version > 180 && HasFunctionFlag( Flags.FunctionFlags.Net ) )
            {
                if( HasFunctionFlag( Flags.FunctionFlags.NetReliable ) )
                {
                    output += "reliable ";
                }
                else
                {
                    output += "unreliable ";
                }

                if( HasFunctionFlag( Flags.FunctionFlags.NetClient ) )
                {
                    output += "client ";
                }

                if( HasFunctionFlag( Flags.FunctionFlags.NetServer ) )
                {
                    output += "server ";
                }
            }

            if( HasFunctionFlag( Flags.FunctionFlags.Native ) )
            {
                output += NativeToken > 0 ? FormatNative() + "(" + NativeToken + ") " : FormatNative() + " ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.Static ) )
            {
                importantOutput += "static ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.Final ) )
            {
                output += "final ";
            }

            // NoExport is no longer available in UE3+ builds,
            // - instead it is replaced with (FunctionFlags.OptionalParameters)
            // - as an indicator that the function has optional parameters.
            if( HasFunctionFlag( Flags.FunctionFlags.NoExport ) && Package.Version <= 220 )
            {
                output += "noexport ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.K2Call ) )
            {
                output += "k2call ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.K2Override ) )
            {
                output += "k2override ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.K2Pure ) )
            {
                output += "k2pure ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.Invariant ) )
            {
                output += "invariant ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.Iterator ) )
            {
                output += "iterator ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.Latent ) )
            {
                output += "latent ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.Singular ) )
            {
                output += "singular ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.Simulated ) )
            {
                output += "simulated ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.Exec ) )
            {
                output += "exec ";
            }

            if( HasFunctionFlag( Flags.FunctionFlags.Event ) )
            {
                output += "event ";
                isNormalFunction = false;
            }

            if( HasFunctionFlag( Flags.FunctionFlags.Delegate ) )
            {
                output += "delegate ";
                isNormalFunction = false;
            }

            if( IsOperator() )
            {
                if( IsPre() )
                {
                    output += "preoperator ";
                }
                else if( IsPost() )
                {
                    output += "postoperator ";
                }
                else
                {
                    output += "operator(" + OperPrecedence + ") ";
                }
                isNormalFunction = false;
            }

            // Don't add function if it's an operator or event or delegate function type!
            if( isNormalFunction )
            {
                output += "function ";
            }
            return "/*"+output+"*/"+importantOutput;
        }

        protected override string FormatHeader()
        {
            string output = String.Empty;
            // static function (string?:) Name(Parms)...
            if( HasFunctionFlag( Flags.FunctionFlags.Native ) )
            {
                // Output native declaration.
                output = String.Format( "// Export U{0}::exec{1}(FFrame&, void* const)\r\n{2}",
                    Outer.Name,
                    Name,
                    UDecompilingState.Tabs
                );
            }

            var metaData = DecompileMeta();
            if( metaData != String.Empty )
                output = metaData + "\r\n" + output;

            var super = (Super != null ? "override " : "virtual ");
            if (HasFunctionFlag(Flags.FunctionFlags.Static))
                super = "";

            var returnType = "void";
            if (ReturnProperty != null)
                returnType = ReturnProperty.GetFriendlyType();
            else if (HasFunctionFlag(Flags.FunctionFlags.Iterator))
            {
                returnType = "";
                var ps = 
                    from p in Params
                    where (p.PropertyFlags & (ulong) Flags.PropertyFlagsLO.OutParm) != 0
                    select p;
                int count = 0;
                foreach (var property in ps)
                {
                    returnType += $"{property.GetFriendlyType()}/* {property.Name}*/,";
                    count++;
                }

                returnType = returnType.Substring(0, returnType.Length - 1);
                if(count > 1)
                    returnType = $"({returnType})";
                returnType = $"System.Collections.Generic.IEnumerable<{returnType}>";
            }

            output += FormatFlags()
                + (ReturnProperty != null
                    ? ReturnProperty.GetFriendlyType() + " "
                    : String.Empty)
                + FriendlyName + FormatParms();
            if( HasFunctionFlag( Flags.FunctionFlags.Const ) )
            {
                output += " const";
            }
            return output;
        }

        private string FormatParms()
        {
            string output = "";
            if( Params != null )
            {
                bool isIterator = HasFunctionFlag(Flags.FunctionFlags.Iterator);
                foreach( var parm in Params )
                {
                    if (parm == ReturnProperty)
                        continue;
                    if (isIterator && (parm.PropertyFlags & (ulong) Flags.PropertyFlagsLO.OutParm) != 0)
                        continue; // Is the iterator's return params
                        
                    output += parm.Decompile() + ", ";
                }

                if (output != "")
                    output = output.Substring(0, output.Length - 2);
            }
            return $"({output})";
        }

        private string FormatCode()
        {
            UDecompilingState.AddTabs( 1 );
            string locals = FormatLocals();
            if( locals != String.Empty )
            {
                locals += "\r\n";
            }
            string code;
            try
            {
                code = DecompileScript();
            }
            catch( Exception e )
            {
                code = e.Message;
            }
            finally
            {
                UDecompilingState.RemoveTabs( 1 );
            }

            if (HasFunctionFlag(Flags.FunctionFlags.Native))
            {
                code += "\r\n\t" + UDecompilingState.Tabs + "#warning NATIVE FUNCTION !";
                if (HasFunctionFlag(Flags.FunctionFlags.Iterator) == false)
                {
                    foreach (UProperty param in Params)
                    {
                        if ((param.PropertyFlags & (ulong) Flags.PropertyFlagsLO.OutParm) != 0 && param != ReturnProperty)
                            code += $"\r\n\t{UDecompilingState.Tabs}{param.Name} = default;";
                    }
                }
                if (HasFunctionFlag(Flags.FunctionFlags.Iterator))
                    code += "\r\n\t" + UDecompilingState.Tabs + "yield return default;";
                if (ReturnProperty != null)
                    code += "\r\n\t" + UDecompilingState.Tabs + "return default;";
            }

            // Empty function!
            if( String.IsNullOrEmpty( locals ) && String.IsNullOrEmpty( code ) )
            {
                return String.Empty;
            }

            return UnrealConfig.PrintBeginBracket() + "\r\n" +
                locals +
                code +
                UnrealConfig.PrintEndBracket();
        }
    }
}
#endif