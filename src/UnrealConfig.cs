﻿using System;
using System.Collections.Generic;
using UELib.Types;

namespace UELib
{
    public static class UnrealConfig
    {
        #region Config
        public static bool StubMode;
        public static bool SuppressComments;
        public static bool SuppressSignature;

        public static string PreBeginBracket = UnrealSyntax.NewLine + "{0}";
        public static string PreEndBracket = UnrealSyntax.NewLine + "{0}";
        public static string Indention = "\t";

        public enum CookedPlatform
        {
            PC,
            Console
        }
        public static CookedPlatform Platform;
        public static Dictionary<string, Tuple<string, PropertyType>> VariableTypes;
        public static readonly List<UnrealPackage> SharedPackages = new List<UnrealPackage>();
        #endregion

        public static string PrintBeginBracket()
        {
            return String.Format( PreBeginBracket, UDecompilingState.Tabs ) + UnrealSyntax.BeginBracket;
        }

        public static string PrintEndBracket()
        {
            return String.Format( PreEndBracket, UDecompilingState.Tabs ) + UnrealSyntax.EndBracket;
        }

        public static string ToUFloat( this float value )
        {
            return value.ToString( "0.0000000000" ).TrimEnd( '0' ).Replace( ',', '.' ) + '0' + 'f';
        }



        public static string OutputWarningException(this Exception e, string introMessage)
        {
            return $"\r\n{UDecompilingState.Tabs}#warning {introMessage}\r\n{UDecompilingState.Tabs}/*{e.ToString().Replace("\n", $"\n{UDecompilingState.Tabs}")} */";
        }
    }
}