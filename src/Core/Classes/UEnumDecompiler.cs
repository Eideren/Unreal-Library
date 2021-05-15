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
            return $"public enum {Name} {DecompileMeta()}";
        }

        private string FormatNames()
        {
            UDecompilingState.AddTabs( 1 );
            string output = $"\r\n{UDecompilingState.Tabs}{string.Join($",\r\n{UDecompilingState.Tabs}", Names)}";
            UDecompilingState.RemoveTabs( 1 );
            return output;
        }
    }
}
#endif