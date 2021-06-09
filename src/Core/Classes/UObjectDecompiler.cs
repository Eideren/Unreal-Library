using System;
using System.Linq;

namespace UELib.Core
{
    public partial class UObject : IUnrealDecompilable
    {
        /// <summary>
        /// Decompiles this Object into human-readable code
        /// </summary>
        public virtual string Decompile()
        {
            if( ShouldDeserializeOnDemand )
            {
                BeginDeserializing();
            }

            string output;
            using (UDecompilingState.TabScope())
            {
                output = DecompileProperties().Replace(';', ',');
            }
            return $"new {Class.Name}\r\n{UDecompilingState.Tabs}{{\r\n{output}{UDecompilingState.Tabs}}}/* Reference: {Class.Name}'{GetOuterGroup()}' */";
        }

        // Ment to be overriden!
        protected virtual string FormatHeader()
        {
            // Note:Dangerous recursive call!
            return Decompile();
        }

        protected string DecompileProperties()
        {
            if( Properties == null || Properties.Count == 0 )
                return "";

            string output = String.Empty;

            #if DEBUG
            output += UDecompilingState.Tabs + "// Object Offset:" + UnrealMethods.FlagToString( (uint)ExportTable.SerialOffset ) + "\r\n";
            #endif

            for( int i = 0; i < Properties.Count; ++ i )
            {
                var propName = Properties[ i ].Name.Name;
                UProperty? prop = ( 
                    from s in ((this as UStruct)??(this as UnknownObject)?.Class as UStruct).EnumerateInheritance()
                    from v in s.Variables
                    where v.Name == propName
                    select v ).FirstOrDefault();

                if( i + 1 < Properties.Count
                    && Properties[ i + 1 ].Name == Properties[ i ].Name
                    && Properties[ i ].ArrayIndex <= 0
                    && Properties[ i + 1 ].ArrayIndex > 0 )
                {
                    string propOutput = "";
                    using( UDecompilingState.TabScope() )
                    {
                        if( Properties[ i ].ArrayIndex != 0 )
                        {
                            propOutput += $"\r\n{UDecompilingState.Tabs}#warning index access seems to hint that the collection is not wholly assigned to, this should probably be changed to assigning to specific indices on the existing collection instead of assigning a whole new collection";
                        }
                        
                        for(int arrayIndex = Properties[ i ].ArrayIndex; 
                            
                            i < Properties.Count 
                            && Properties[ i ].Name == propName 
                            && Properties[ i ].ArrayIndex == arrayIndex; 
                            
                            i++, arrayIndex++ )
                        {
                            propOutput += $"\r\n{UDecompilingState.Tabs}[{arrayIndex}] = {Properties[i].Decompile(true)},";
                        }
                    }
                    i--;
                    
                    output += $"{UDecompilingState.Tabs}{propName} = new {prop?.GetFriendlyType()}()\r\n{UDecompilingState.Tabs}{{ {propOutput}\r\n {UDecompilingState.Tabs}}};\r\n";
                    continue;
                }

                if( prop.IsArray )
                    propName += "[0]";
                
                // FORMAT: 'DEBUG[TAB /* 0xPOSITION */] TABS propertyOutput + NEWLINE
                output += UDecompilingState.Tabs +
#if DEBUG_POSITIONS
            "/*" + UnrealMethods.FlagToString( (uint)Properties[i]._BeginOffset ) + "*/\t" +
#endif
                    $"{propName} = {Properties[ i ].Decompile( true )};\r\n";
            }
            return output;
        }
    }
}