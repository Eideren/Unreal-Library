#if DECOMPILE
using System;
using System.Collections.Generic;
using System.Linq;

namespace UELib.Core
{
    public partial class UState
    {
        /// <summary>
        /// Decompiles this object into a text format of:
        ///
        /// [FLAGS] state[()] NAME [extends NAME]
        /// {
        ///     [ignores Name[,Name];]
        ///
        ///     [FUNCTIONS]
        ///
        /// [STATE CODE]
        /// };
        /// </summary>
        /// <returns></returns>
        public override string Decompile()
        {
            string content = FormatHeader() + UnrealConfig.PrintBeginBracket();
            UDecompilingState.AddTabs( 1 );

                var locals = FormatLocals();
                if( locals != String.Empty )
                {
                    content += "\r\n" + locals;
                }

                content += FormatIgnores() +
                    FormatConstants() +
                    FormatFunctions() +
                    DecompileScript();
            UDecompilingState.RemoveTabs( 1 );
            content += UnrealConfig.PrintEndBracket();
            return content;
        }

        private string GetAuto()
        {
            return HasStateFlag( Flags.StateFlags.Auto ) ? "auto " : String.Empty;
        }

        private string GetSimulated()
        {
            return HasStateFlag( Flags.StateFlags.Simulated ) ? "simulated " : String.Empty;
        }

        private string GetEdit()
        {
            return HasStateFlag( Flags.StateFlags.Editable ) ? "()" : String.Empty;
        }

        protected override string FormatHeader()
        {
            string output = GetAuto() + GetSimulated() + "state" + GetEdit() + " " + Name;
            if( Super != null && Super.Name != Name
                /* Not the same because when overriding states it automatic extends the parent state */ )
            {
                output += " " + FormatExtends() + " " + Super.Name;
            }

            output = $"protected virtual System.Collections.Generic.IEnumerable<Flow> {Name}(string jumpTo = null)/*{output}*/";
            
            return output;
        }

        private string FormatIgnores()
        {
            if( _IgnoreMask == long.MaxValue || Functions == null || !Functions.Any() )
            {
                return String.Empty;
            }

            string output = "\r\n" + UDecompilingState.Tabs + "/*ignores*/";
            var ignores = new List<string>();
            foreach( var func in Functions.Where( func => !func.HasFunctionFlag( Flags.FunctionFlags.Defined ) ) )
            {
                ignores.Add( func.Name );
            }

            for( int i = 0; i < ignores.Count; ++ i )
            {
                output += $"{ignores[i]} = () => {{}};";
            }
            return ignores.Count > 0 ? output : String.Empty;
        }

        protected string FormatFunctions()
        {
            if( Functions == null || !Functions.Any() )
                return String.Empty;

            // Remove functions from parent state, e.g. when overriding states.
            var formatFunctions = GetType() == typeof(UState)
                ? Functions.Where( f => f.HasFunctionFlag( Flags.FunctionFlags.Defined ) )
                : Functions;

            string output = String.Empty;
            foreach( var scriptFunction in formatFunctions )
            {
                try
                {
                    string functionOutput = "\r\n" + UDecompilingState.Tabs + scriptFunction.Decompile() + "\r\n";
                    output += functionOutput;
                }
                catch( Exception e )
                {
                    output += "\r\n" + UDecompilingState.Tabs + "// F:" + scriptFunction.Name + " E:" + e;
                }
            }
            return output;
        }
    }
}
#endif