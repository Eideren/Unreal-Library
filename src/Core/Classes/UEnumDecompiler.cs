#if DECOMPILE
using System;

namespace UELib.Core
{
    public partial class UEnum
    {
        /// <summary>
        /// Decompiles this object into a text format of:
        ///
        /// enum NAME
        /// {
        ///     [ELEMENTS]
        /// };
        /// </summary>
        /// <returns></returns>
        public override string Decompile()
        {
            return UDecompilingState.Tabs + FormatHeader() +
                UnrealConfig.PrintBeginBracket() +
                FormatNames() +
                UnrealConfig.PrintEndBracket()  + ";";
        }

        protected override string FormatHeader()
        {
            return "public struct /*enum*/ " + Name + DecompileMeta();
        }

        private string FormatNames()
        {
            string output = String.Empty;
            UDecompilingState.AddTabs( 1 );
            for( int index = 0; index < Names.Count; index++ )
            {
                var enumName = Names[index];
                output += "\r\n" + UDecompilingState.Tabs + $"public static {Name} {enumName} => {index};";
            }

            output += "\r\n" + UDecompilingState.Tabs + $"private static readonly string[] IndexToValue = {{ \"{string.Join("\", \"", Names)}\" }};";

            output += "\r\n";
            output += "\r\n" + UDecompilingState.Tabs + $"public int Value;";
            output += "\r\n" + UDecompilingState.Tabs + $"public {Name}(int v) => Value = v;";
            output += "\r\n" + UDecompilingState.Tabs + $"public static implicit operator int({Name} v) => v.Value;";
            output += "\r\n" + UDecompilingState.Tabs + $"public static implicit operator {Name}(int v) => new {Name}(v);";
            output += "\r\n" + UDecompilingState.Tabs + $"public static implicit operator {Name}(byte v) => new {Name}(v);";
            output += "\r\n" + UDecompilingState.Tabs + $"public static explicit operator string({Name} v) => IndexToValue[v.Value];";
            
            UDecompilingState.RemoveTabs( 1 );
            return output;
        }
    }
}
#endif