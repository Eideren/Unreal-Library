using System.Text;
#if DECOMPILE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UELib.Core
{
    public partial class UClass
    {
        protected override string CPPTextKeyword => "cpptext";

        /**
         * Structure looks like this, even though said XX.GetFriendlyName() actually it's XX.Decompile() which handles then the rest on its own.
         * class GetName() extends SuperFieldName
         *      FormatFlags()
         *
         * CPPTEXT
         * {
         * }
         *
         * Constants
         * const C.GetFriendlyName() = C.Value
         *
         * Enums
         * enum En.GetFriendlyName()
         * {
         *      FormatProperties()
         * }
         *
         * Structs
         * struct FormatFlags() Str.GetFriendlyName() extends SuperFieldName
         * {
         *      FormatProperties()
         * }
         *
         * Properties
         * var(GetCategoryName) FormatFlags() Prop.GetFriendlyName()
         *
         * Replication
         * {
         *      SerializeToken()
         * }
         *
         * Functions
         * FormatFlags() GetFriendlyName() GetParms()
         * {
         *      FormatLocals()
         *      SerializeToken()
         * }
         *
         * States
         * FormatFlags() state GetFriendlyName() extends SuperFieldName
         * {
         *      FormatIgnores()
         *      FormatFunctions()
         *      SerializeToken();
         * }
         *
         * DefaultProperties
         * {
         * }
         */
        public override string Decompile()
        {
            var content = "";

            content += FormatHeader() +
                       FormatCPPText() +
                       FormatConstants() +
                       FormatEnums() +
                       FormatStructs() +
                       FormatWithin() +
                       FormatProperties() +
                       FormatReplication() +
                       FormatFunctionsOuter() +
                       FormatStates() +
                       FormatDefaultProperties()+"\n}";

            return content;
        }

        public string FormatWithin()
        {
            if( IsClassWithin() )
            {
                return $"\r\n{UDecompilingState.Tabs}public new {Within.Name} Outer => base.Outer as {Within.Name};\r\n";
            }

            return "";
        }

        public string GetDependencies()
        {
            if( ClassDependencies == null )
                return string.Empty;

            string output = string.Empty;
            foreach( var dep in ClassDependencies )
            {
                var obj = GetIndexObject( dep.Class );
                if( obj != null )
                {
                    output += " *\t" + obj.GetClassName() + " " + obj.GetOuterGroup() + "\r\n";
                }
            }
            return output.Length != 0 ? "Class Dependencies:\r\n" + output + " *" : string.Empty;
        }

        private string GetImports()
        {
            if( PackageImports == null )
                return string.Empty;

            string output = string.Empty;
            foreach( int packageImport in PackageImports )
            {
                output += " *\t" + Package.Names[packageImport].Name + "\r\n";
                /*for( int j = 1; j < (i + i) && (j + (i + i)) < PackageImportsList.Count; ++ j )
                {
                    Output += " *\t\t\t" + Owner.NameTableList[PackageImportsList[i + j]].Name + "\r\n";
                }
                i += i;*/
            }
            return output.Length != 0 ? "Package Imports:\r\n" + output + " *" : string.Empty;
        }

        public string GetStats()
        {
            string output = string.Empty;

            if( Constants != null && Constants.Count > 0 )
                output += " *\tConstants:" + Constants.Count + "\r\n";

            if( Enums != null && Enums.Count > 0 )
                output += " *\tEnums:" + Enums.Count + "\r\n";

            if( Structs != null && Structs.Count > 0 )
                output += " *\tStructs:" + Structs.Count + "\r\n";

            if( Variables != null && Variables.Count > 0 )
                output += " *\tProperties:" + Variables.Count + "\r\n";

            if( Functions != null && Functions.Count > 0 )
                output += " *\tFunctions:" + Functions.Count + "\r\n";

            if( States != null && States.Count > 0 )
                output += " *\tStates:" + States.Count + "\r\n";

            return output.Length != 0 ? "Stats:\r\n" + output + " *" : string.Empty;
        }

        protected override string FormatHeader()
        {
            string output = (IsClassInterface() ? "public interface " : "public partial class ") + Name;
            var metaData = DecompileMeta();
            if( metaData != string.Empty )
            {
                output = metaData + "\r\n" + output;
            }

            bool hasSuper = false;
            // Object doesn't have an extension so only try add the extension if theres a SuperField
            if( Super != null
                && !(IsClassInterface() && string.Compare( Super.Name, "Object", StringComparison.OrdinalIgnoreCase ) == 0) )
            {
                output += " : " + Super.GetFriendlyType();
                hasSuper = true;
            }

            if (ImplementedInterfaces?.Count > 0)
            {
                output += hasSuper ? ", " :  " : ";
                output += FormatInterfaces( "implements", ImplementedInterfaces );
            }

            if( IsClassWithin() )
            {
                output += "/* within " + Within.Name + "*/";
            }

            string rules = FormatFlags().Replace( "\t", UnrealConfig.Indention );
            return $"{output}{rules}{{";
        }

        private string FormatNameGroup( string groupName, IList<int> enumerableList )
        {
            string output = string.Empty;
            if( enumerableList != null && enumerableList.Any() )
            {
                output += "\r\n\t" + groupName + "(";
                try
                {
                    foreach( int index in enumerableList )
                    {
                        output += Package.Names[index].Name + ",";
                    }
                    output = output.TrimEnd( ',' ) + ")";
                }
                catch
                {
                    output += string.Format( "\r\n\t/* An exception occurred while decompiling {0}. */", groupName );
                }
            }
            return output;
        }

        private string FormatInterfaces( string groupName, IList<int> enumerableList )
        {
            string output = string.Empty;
            if( enumerableList != null && enumerableList.Any() )
            {
                output += "\r\n\t";
                try
                {
                    foreach( int index in enumerableList )
                    {
                        var i = Package.GetIndexObjectName(index);
                        if (i == "Object")
                            i = "Core.Object";
                        output += i + ",";
                    }
                    output = output.TrimEnd( ',' );
                }
                catch
                {
                    output += string.Format( "\r\n\t/* An exception occurred while decompiling {0}. */", groupName );
                }
            }
            return output;
        }

        private string FormatFlags()
        {
            string output = string.Empty;

            if( (ClassFlags & (uint)Flags.ClassFlags.Abstract) != 0 )
            {
                output += "\r\n\tabstract";
            }

            if( (ClassFlags & (uint)Flags.ClassFlags.Transient) != 0 )
            {
                output += "\r\n\ttransient";
            }
            else
            {
                // Only do if parent had Transient
                UClass parentClass = (UClass)Super;
                if( parentClass != null && (parentClass.ClassFlags & (uint)Flags.ClassFlags.Transient) != 0 )
                {
                    output += "\r\n\tnotransient";
                }
            }

            if( HasObjectFlag( Flags.ObjectFlagsLO.Native ) )
            {
                output += "\r\n\t" + FormatNative();
                if( NativeClassName.Length != 0 )
                {
                    output += "(" + NativeClassName + ")";
                }
            }

            if( HasClassFlag( Flags.ClassFlags.NativeOnly ) )
            {
                output += "\r\n\tnativeonly";
            }

            if( HasClassFlag( Flags.ClassFlags.NativeReplication ) )
            {
                output += "\r\n\tnativereplication";
            }

            // BTClient.Menu.uc has Config(ClientBtimes) and this flag is not true???
            if( (ClassFlags & (uint)Flags.ClassFlags.Config) != 0 )
            {
                string inner = ConfigName;
                if( string.Compare( inner, "None", StringComparison.OrdinalIgnoreCase ) == 0
                    || string.Compare( inner, "System", StringComparison.OrdinalIgnoreCase ) == 0 )
                {
                    output += "\r\n\tconfig";
                }
                else output += "\r\n\tconfig(" + inner + ")";
            }

            if( (ClassFlags & (uint)Flags.ClassFlags.ParseConfig) != 0 )
            {
                output += "\r\n\tparseconfig";
            }

            if( (ClassFlags & (uint)Flags.ClassFlags.PerObjectConfig) != 0 )
            {
                output += "\r\n\tperobjectconfig";
            }
            else
            {
                // Only do if parent had PerObjectConfig
                UClass parentClass = (UClass)Super;
                if( parentClass != null && (parentClass.ClassFlags & (uint)Flags.ClassFlags.PerObjectConfig) != 0 )
                {
                    output += "\r\n\tnoperobjectconfig";
                }
            }

            if( (ClassFlags & (uint)Flags.ClassFlags.EditInlineNew) != 0 )
            {
                output += "\r\n\teditinlinenew";
            }
            else
            {
                // Only do if parent had EditInlineNew
                UClass parentClass = (UClass)Super;
                if( parentClass != null && (parentClass.ClassFlags & (uint)Flags.ClassFlags.EditInlineNew) != 0 )
                {
                    output += "\r\n\tnoteditinlinenew";
                }
            }

            if( (ClassFlags & (uint)Flags.ClassFlags.CollapseCategories) != 0 )
            {
                output += "\r\n\tcollapsecategories";
            }

            // TODO: Might indicate "Interface" in later versions
            if( HasClassFlag( Flags.ClassFlags.ExportStructs ) && Package.Version < 300 )
            {
                output += "\r\n\texportstructs";
            }

            if( (ClassFlags & (uint)Flags.ClassFlags.NoExport) != 0 )
            {
                output += "\r\n\tnoexport";
            }

            if( Extends( "Actor" ) )
            {
                if( (ClassFlags & (uint)Flags.ClassFlags.Placeable) != 0 )
                {
                    output += Package.Version >= PlaceableVersion ? "\r\n\tplaceable" : "\r\n\tusercreate";
                }
                else
                {
                    output += Package.Version >= PlaceableVersion ? "\r\n\tnotplaceable" : "\r\n\tnousercreate";
                }
            }

            if( (ClassFlags & (uint)Flags.ClassFlags.SafeReplace) != 0 )
            {
                output += "\r\n\tsafereplace";
            }

            // Approx version
            if( (ClassFlags & (uint)Flags.ClassFlags.Instanced) != 0 && Package.Version < 150 )
            {
                output += "\r\n\tinstanced";
            }

            if( (ClassFlags & (uint)Flags.ClassFlags.HideDropDown) != 0 )
            {
                output += "\r\n\thidedropdown";
            }

            if( Package.Build == UnrealPackage.GameBuild.BuildName.UT2004 )
            {
                if( HasClassFlag( Flags.ClassFlags.CacheExempt ) )
                {
                    output += "\r\n\tcacheexempt";
                }
            }

            if( Package.Version >= 749 && Super != null )
            {
                if( ForceScriptOrder && !((UClass)Super).ForceScriptOrder )
                {
                    output += "\r\n\tforcescriptorder(true)";
                }
                else if( !ForceScriptOrder && ((UClass)Super).ForceScriptOrder )
                    output += "\r\n\tforcescriptorder(false)";
            }

            try
            {
                if( Package.Version >= UnrealPackage.VDLLBIND
                    && string.Compare( DLLBindName, "None", StringComparison.OrdinalIgnoreCase ) != 0 )
                {
                    output += "\r\n\tdllbind(" + DLLBindName + ")";
                }
            }
            catch
            {
                output += "\r\n\t// Failed to decompile dllbind";
            }

            if( ClassDependencies != null )
            {
                var dependsOn = new List<int>();
                foreach( var dependency in ClassDependencies )
                {
                    if( dependsOn.Exists( dep => dep == dependency.Class ) )
                    {
                        continue;
                    }
                    var obj = (UClass)GetIndexObject( dependency.Class );
                    // Only exports and those who are further than this class
                    if( obj != null && (int)obj > (int)this )
                    {
                        output += "\r\n\tdependson(" + obj.Name + ")";
                    }
                    dependsOn.Add( dependency.Class );
                }
            }

            output += FormatNameGroup( "dontsortcategories", DontSortCategories );
            output += FormatNameGroup( "hidecategories", HideCategories );
            output += FormatNameGroup( "classgroup", ClassGroups );
            output += FormatNameGroup( "autoexpandcategories", AutoExpandCategories );
            output += FormatNameGroup( "autocollapsecategories", AutoCollapseCategories );
            if (string.IsNullOrWhiteSpace(output) == false)
                output = $"/*{output}*/";

            return output;
        }

        const ushort VReliableDeprecation = 189;

        public string FormatReplication()
        {
            if( DataScriptSize <= 0 )
            {
                return string.Empty;
            }

            var replicatedObjects = new List<IUnrealNetObject>();
            if( Variables != null )
            {
                replicatedObjects.AddRange( Variables.Where( prop => prop.HasPropertyFlag( Flags.PropertyFlagsLO.Net ) && prop.RepOffset != ushort.MaxValue ) );
            }

            if( Package.Version < VReliableDeprecation && Functions != null )
            {
                replicatedObjects.AddRange( Functions.Where( func => func.HasFunctionFlag( Flags.FunctionFlags.Net ) && func.RepOffset != ushort.MaxValue ) );
            }

            if( replicatedObjects.Count == 0 )
            {
                return string.Empty;
            }

            var statements = new Dictionary<uint, List<IUnrealNetObject>>();
            replicatedObjects.Sort( (ro, ro2) => ro.RepKey.CompareTo( ro2.RepKey ) );
            for( int netIndex = 0; netIndex < replicatedObjects.Count; ++ netIndex )
            {
                var firstObject = replicatedObjects[netIndex];
                var netObjects = new List<IUnrealNetObject>{firstObject};
                for( int nextIndex = netIndex + 1; nextIndex < replicatedObjects.Count; ++ nextIndex )
                {
                    var nextObject = replicatedObjects[nextIndex];
                    if( nextObject.RepOffset != firstObject.RepOffset
                        || nextObject.RepReliable != firstObject.RepReliable
                        )
                    {
                        netIndex = nextIndex - 1;
                        break;
                    }
                    netObjects.Add( nextObject );
                }

                netObjects.Sort( (o, o2) => string.Compare( o.Name, o2.Name, StringComparison.Ordinal ) );
                if( !statements.ContainsKey( firstObject.RepKey ) )
                    statements.Add( firstObject.RepKey, netObjects );
            }
            replicatedObjects.Clear();

            var output = new StringBuilder( "\r\n" + "replication" +  UnrealConfig.PrintBeginBracket() ); 
            
            using (UDecompilingState.TabScope()){

            foreach( var statement in statements )
            {
                try
                {
                    var pos = (ushort)(statement.Key & 0x0000FFFF);
                    var rel = Convert.ToBoolean( statement.Key & 0xFFFF0000 );

                    output.Append( "\r\n" + UDecompilingState.Tabs );
                    if( !UnrealConfig.SuppressComments )
                    {
                        output.AppendFormat( "// Pos:0x{0:X3}\r\n{1}", pos, UDecompilingState.Tabs );
                    }

                    ByteCodeManager.Deserialize();
                    ByteCodeManager.JumpTo( pos );
                    string statementCode;
                    try
                    {
                        statementCode = ByteCodeManager.CurrentToken.Decompile();
                    }
                    catch( Exception e )
                    {
                        statementCode = string.Format( "/* An exception occurred while decompiling condition ({0}) */", e );
                    }
                    var statementType = Package.Version < VReliableDeprecation ? rel ? "reliable" : "unreliable" : string.Empty;
                    var statementFormat = string.Format( "{0} if({1})", statementType, statementCode );
                    output.Append( statementFormat );

                    using (UDecompilingState.TabScope())
                    {
                        // NetObjects
                        for( int i = 0; i < statement.Value.Count; ++i )
                        {
                            var shouldSplit = i % 2 == 0;
                            if( shouldSplit )
                            {
                                output.Append( "\r\n" + UDecompilingState.Tabs );
                            }

                            var netObject = statement.Value[i];
                            output.Append( netObject.Name );

                            var isNotLast = i != statement.Value.Count - 1;
                            output.Append( isNotLast ? ", " : ";" );
                        }
                    }

                    // IsNotLast
                    if( statements.Last().Key != statement.Key )
                    {
                        output.Append( "\r\n" );
                    }
                }
                catch( Exception e )
                {
                    output.AppendFormat( "/* An exception occurred while decompiling a statement! ({0}) */", e );
                }
            }
            }
            
            output.Append( UnrealConfig.PrintEndBracket() );
            output.Replace("\n", "\n//");
            output.Append("\r\n");
            return output.ToString();
        }

        private string FormatStates()
        {
            if( States == null || !States.Any() )
                return string.Empty;

            string output = string.Empty;
            foreach( var scriptState in States )
            {
                output += "\r\n" + scriptState.Decompile() + "\r\n";
            }

            if (GetType() != typeof(UState) && States?.Count > 0)
            {
                // add/override FindState function to include this class' states with it

                output += $"{UDecompilingState.Tabs}protected override (System.Action<name>, StateFlow, System.Action<name>) FindState(name stateName)\r\n{{\r\n";

                using (UDecompilingState.TabScope())
                {
                    output += $"{UDecompilingState.Tabs}switch(stateName)\r\n";
                    output += $"{UDecompilingState.Tabs}{{\r\n";

                    using (UDecompilingState.TabScope())
                    {
                        foreach (var state in States)
                            output += $"{UDecompilingState.Tabs}case \"{state.Name}\": return {state.Name}();\r\n";
                        output += $"{UDecompilingState.Tabs}default: return base.FindState(stateName);\r\n";
                    }

                    output += $"{UDecompilingState.Tabs}}}\r\n";
                }
                
                output += $"{UDecompilingState.Tabs}}}";
            }

            var auto = (
                from x in States
                where x.HasStateFlag(Flags.StateFlags.Auto)
                select x
            );
            
            if (auto.Any())
            {
                var inner = "";
                foreach (var v in auto)
                    inner += $"\n\treturn {v.Name}();";
                if (auto.Count() > 1)
                    inner += "\n\t#warning multiple autos";
                output += $@"
protected override (System.Action<name>, StateFlow, System.Action<name>) GetAutoState()
{{
{inner}
}}";
            }

            return output;
        }
    }
}
#endif