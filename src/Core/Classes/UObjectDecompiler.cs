using System;

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
            if(Class.Name == "UITexture")
                System.Diagnostics.Debugger.Break();
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
                if( i + 1 < Properties.Count
                    && Properties[ i + 1 ].Name == Properties[ i ].Name
                    && Properties[ i ].ArrayIndex <= 0
                    && Properties[ i + 1 ].ArrayIndex > 0 )
                {
                    string propOutput = "";
                    var arrayVarName = Properties[ i ].Name;
                    using( UDecompilingState.TabScope() )
                    {
                        for(int increasingIndex = Properties[ i ].ArrayIndex; 
                            
                            i < Properties.Count 
                            && Properties[ i ].Name == arrayVarName 
                            && Properties[ i ].ArrayIndex == increasingIndex; 
                            
                            i++, increasingIndex++ )
                        {
                            propOutput += $"\r\n{UDecompilingState.Tabs}[{i}] = {Properties[i].Decompile(true)},";
                        }
                    }
                    i--;

                    output += $"{UDecompilingState.Tabs}{arrayVarName} = new()\r\n{UDecompilingState.Tabs}{{ {propOutput}\r\n {UDecompilingState.Tabs}}};\r\n";
                    continue;
                }
                
                
                
            
                /*if( arrayindex == String.Empty 
                    && _Container.Class is UClass uclass
                    && ( from s in uclass.EnumerateInheritance()
                        where s is UClass
                        from v in ( (UClass) s ).Variables
                        where v.Name == Name.Name
                        select v ).FirstOrDefault() is UProperty matchingVariable && matchingVariable.IsArray )
                {
                    value = $"new []{{ {value} }}";
                }*/
                
                
                // FORMAT: 'DEBUG[TAB /* 0xPOSITION */] TABS propertyOutput + NEWLINE
                output += UDecompilingState.Tabs +
#if DEBUG_POSITIONS
            "/*" + UnrealMethods.FlagToString( (uint)Properties[i]._BeginOffset ) + "*/\t" +
#endif
                    Properties[i].Decompile() + "\r\n";
            }
            return output;
        }
    }
}