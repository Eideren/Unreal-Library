#if DECOMPILE
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace UELib.Core
{
    public partial class UFunction
    {
        public bool OverridenByState
        {
            get
            {
                for (var s = this; s != null; s = s.Super as UFunction)
                {
                    if (s.SelfOverridenByState)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool SelfOverridenByState;

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
            string header = FormatHeader();
            string code;
            try
            {
                code = FormatCode();
            }
            catch( Exception e )
            {
                code = e.Message;
            }
            
            return header + code;
        }

        public string NameOfSpecificFunctionImplementation
        {
            get
            {
                if (Outer.GetType() == typeof(UState))
                    return AsFullyQualifiedStateName;
                if (OverridenByState)
                    return AsFullyQualifiedStateName;
                return Name;
            }
        }

        public string AsFullyQualifiedStateName 
        {
            get
            {
                if(Outer?.Outer != null)
                    return $"{Outer.Outer.Name}_{Outer.Name}_{Name}";
                else if(Outer != null)
                    return $"{Outer.Name}_{Name}";
                return Name;
            }
        }
        
        public bool IsStateFunction => Outer.GetType() == typeof(UState);

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
                importantOutput += "delegate ";
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

            if (output.Length > 0)
                output = $"/*{output}*/";
            return output+importantOutput;
        }

        public string EmptyInlineDeclaration()
        {
            var parameters = FormatParms();
            int index = 0;
            var paramsAsDiscard = string.Join("", (
                from c in parameters
                where c == ','
                select "_"+(++index).ToString()+c));
            if (string.IsNullOrWhiteSpace(parameters) == false)
                paramsAsDiscard += "_a";
            return $"({paramsAsDiscard})=>{(ReturnType() != "void" ? "default" : "{}" )}";
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

            var overridingType = (Super != null ? "override" : "virtual");
            if (HasFunctionFlag(Flags.FunctionFlags.Delegate) || IsStateFunction || HasFunctionFlag(Flags.FunctionFlags.Static) || (this.Outer as UClass).IsClassInterface())
                overridingType = "";



            var signatureSource = this;
            var returnType = signatureSource.ReturnType();

            if(OverridenByState && Outer is UClass /*Ignore for in-state defined functions*/)
            {
                if(Super == null)
                    output += $"public delegate {returnType} {Name}_del({signatureSource.FormatParms()});\r\n";
                output += $"public {overridingType} {Name}_del {Name} {{ get => bfield_{Name} ?? {NameOfSpecificFunctionImplementation}; set => bfield_{FriendlyName} = value; }} {FriendlyName}_del bfield_{FriendlyName};\r\n";
                output += $"public {overridingType} {Name}_del global_{Name} => {NameOfSpecificFunctionImplementation};\r\n";
                overridingType = "";
            }

            overridingType = string.IsNullOrEmpty( overridingType ) ? "" : overridingType + " ";
            output += $"{(Outer is UClass ? "public" : "protected" )} {overridingType}{FormatFlags()}{returnType} {NameOfSpecificFunctionImplementation}({signatureSource.FormatParms()})";

            if( HasFunctionFlag( Flags.FunctionFlags.Const ) )
            {
                output += " const";
            }

            if (IsStateFunction)
                output += "// state function";
            
            return output;
        }

        public string ReturnType()
        {
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
            else if (HasFunctionFlag(Flags.FunctionFlags.Latent))
            {
                returnType = "Flow";
            }

            return returnType;
        }

        public string FormatParms()
        {
            string output = "";
            if( Params != null )
            {
                bool isIterator = HasFunctionFlag(Flags.FunctionFlags.Iterator);
                int indexOfLastOut = 0;
                for( indexOfLastOut = Params.Count - 1; indexOfLastOut >= 0; indexOfLastOut-- )
                {
                    var parm = Params[ indexOfLastOut ];
                    if (parm == ReturnProperty)
                        continue;
                    if((parm.PropertyFlags & (ulong) Flags.PropertyFlagsLO.OutParm) != 0)
                        break;
                }

                for( int i = 0; i < Params.Count; i++ )
                {
                    var parm = Params[ i ];
                    if (parm == ReturnProperty)
                        continue;
                    if (isIterator && (parm.PropertyFlags & (ulong) Flags.PropertyFlagsLO.OutParm) != 0)
                        continue; // Is the iterator's return params

                    var decompiled = parm.Decompile();
                    if( i < indexOfLastOut && decompiled.EndsWith( " = default" ) )
                        decompiled = decompiled.Replace( " = default", "/* = default*/" );
                    output += $"{decompiled}, ";
                }

                if (output != "")
                    output = output.Substring(0, output.Length - 2);
            }
            return $"{output}";
        }

        private string FormatCode()
        {
            bool forceReturn = UnrealConfig.StubMode;
            bool forceDefaultOut = UnrealConfig.StubMode;
            if (UnrealConfig.StubMode && (this.Outer as UClass)?.IsClassInterface() == true)
                return ";";
            
            string code;
            string locals;
            using( UDecompilingState.TabScope() )
            {
                locals = UnrealConfig.StubMode ? "" : FormatLocals();
                if( locals != String.Empty )
                {
                    locals += "\r\n";
                }
                try
                {
                    code = UnrealConfig.StubMode ? "" : DecompileScript();
                }
                catch( Exception e )
                {
                    code = e.Message;
                }
            }

            if (HasFunctionFlag(Flags.FunctionFlags.Delegate) && string.IsNullOrEmpty(code))
                return ";";

            if (HasFunctionFlag(Flags.FunctionFlags.Native))
            {
                if( string.IsNullOrWhiteSpace( code ) == false )
                    code += "\r\n";
                code += UDecompilingState.Tabs + "\t // #warning NATIVE FUNCTION !";
                forceDefaultOut = true;
                forceReturn = true;
            }

            // Empty function!
            if( String.IsNullOrEmpty( locals ) && String.IsNullOrEmpty( code ) )
            {
                forceReturn = true;
            }

            /* Unreal's Out is actually a pass by reference, therefore the function might actually only read from it, we shouldn't set default
            if (forceDefaultOut)
            {
                if (HasFunctionFlag(Flags.FunctionFlags.Iterator) == false)
                {
                    foreach (UProperty param in Params)
                    {
                        if ((param.PropertyFlags & (ulong) Flags.PropertyFlagsLO.OutParm) != 0 && param != ReturnProperty)
                            code += $"\r\n\t{UDecompilingState.Tabs}{param.Name} = default;";
                    }
                }
            }*/

            using( UDecompilingState.TabScope() )
            {
                if( UnrealConfig.StubMode )
                {   
                    if( string.IsNullOrWhiteSpace( code ) == false )
                        code += "\r\n";
                    code += $"{UDecompilingState.Tabs}// stub";
                }
            }

            {
                int balance = 0;
                foreach (char c in code)
                {
                    if (c == '{')
                        balance += 1;
                    else if (c == '}')
                        balance -= 1;
                }

                if (balance > 0)
                {
                    code += $"\r\n{UDecompilingState.Tabs}#warning Force closing unclosed braces, verify that the logic therein is valid";
                    do
                    {
                        code += $"\r\n{UDecompilingState.Tabs}}}";
                    } while (--balance > 0);
                }
            }

            if (forceReturn)
            {
                if (HasFunctionFlag(Flags.FunctionFlags.Iterator))
                    code += "\r\n\t" + UDecompilingState.Tabs + "yield break;";
                if (HasFunctionFlag(Flags.FunctionFlags.Latent))
                    code += "\r\n\t" + UDecompilingState.Tabs + "return default;";
                if (ReturnProperty != null)
                    code += "\r\n\t" + UDecompilingState.Tabs + "return default;";
            }

            return UnrealConfig.PrintBeginBracket() + "\r\n" +
                   locals +
                   code +
                   UnrealConfig.PrintEndBracket();
        }
    }
}
#endif