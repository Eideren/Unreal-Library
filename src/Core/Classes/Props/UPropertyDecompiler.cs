﻿#if DECOMPILE
using System;
using System.Globalization;
using System.Linq;

namespace UELib.Core
{
    using System.Collections.Generic;



    public partial class UProperty
    {
        // Called before the var () is printed.
        public virtual string PreDecompile()
        {
            string tooltipValue = null;
            MetaData?.Tags.TryGetValue("ToolTip", out tooltipValue);
            if (tooltipValue == null)
            {
                return string.Empty;
            }

            string comment = UDecompilingState.Tabs + "/** ";
            // Multiline comment?
            if( tooltipValue.IndexOf( '\n' ) != -1 )
            {
                comment += " \r\n" + UDecompilingState.Tabs + " *"
                           + tooltipValue.Replace( "\n", "\n" + UDecompilingState.Tabs + " *" )
                           + "\r\n" + UDecompilingState.Tabs;
            }
            else
            {
                comment += tooltipValue;
            }
            return comment + " */\r\n";
        }



        public bool HasSpecificDefaultValue()
        {
            if( Package.Version <= 300 
                || ( PropertyFlags & (ulong) Flags.PropertyFlagsLO.OptionalParm ) == 0
                || ( PropertyFlags & (ulong) Flags.PropertyFlagsLO.OutParm ) != 0)
                return false;
            return true;
            
            if( Outer is UFunction f )
            {
                f.ByteCodeManager.Deserialize();
                int optionalBeforeThisOne = 0;
                foreach( var param in f.Params )
                {
                    if( ReferenceEquals( this, param ) )
                        break;
                    if( param.HasPropertyFlag( Flags.PropertyFlagsLO.OptionalParm ) )
                        optionalBeforeThisOne++;
                }
                    
                foreach( var token in f.ByteCodeManager.DeserializedTokens )
                {
                    if( token is UStruct.UByteCodeDecompiler.DefaultParameterToken 
                        || token is UStruct.UByteCodeDecompiler.NothingToken )
                    {
                        if( optionalBeforeThisOne == 0 )
                            return true;
                        optionalBeforeThisOne--;
                    }
                }
            }

            return false;
        }



        public override string Decompile()
        {
            string postfix = "";
            string name = Name;
            if( Package.Version > 300 && ( PropertyFlags & (ulong) Flags.PropertyFlagsLO.OptionalParm ) != 0 )
            {
                if( HasSpecificDefaultValue() )
                    name = $"_{name}";
                postfix = (PropertyFlags & (ulong) Flags.PropertyFlagsLO.OutParm) != 0 ? "/* = default*/" : " = default";
            }
            
            return $"{FormatFlags()}{GetFriendlyType()} {name}{postfix}{DecompileMeta()}";
        }



        public override string GetFriendlyType()
        {
            var concInter = HasSpecificDefaultValue() 
                            && this is UObjectProperty == false 
                            && this is UDelegateProperty == false 
                            && this is UInterfaceProperty == false 
                ? "?" : "";
            var type = GetFriendlyPropType() ?? base.GetFriendlyType();
            if( IsArray )
            {
                string arraySizeDecl = ArrayEnum != null 
                    ? ArrayEnum.ParseAsEnum( ArrayEnum.Names.Count - 1, false ) 
                    : ArrayDim.ToString( CultureInfo.InvariantCulture );
                return $"StaticArray<{string.Join( ", ", Enumerable.Repeat( type, ArrayDim ) )}>/*[{arraySizeDecl}]*/{concInter}";
            }
            else
            {
                return type + concInter;
            }
        }



        public virtual string GetFriendlyPropType() => null;



        private string FormatSize(bool withBracket = true)
        {
            if( !IsArray )
            {
                return string.Empty;
            }

            string arraySizeDecl = ArrayEnum != null 
                ? $"(int){ArrayEnum.ParseAsEnum( ArrayEnum.Names.Count - 1 )}" 
                : ArrayDim.ToString( CultureInfo.InvariantCulture );
            return withBracket ? $"[{arraySizeDecl}]" : arraySizeDecl;
        }

        private string FormatAccess()
        {
            var output = string.Empty;

            // none are true in StreamInteraction.uc???
            if( IsPrivate() )
            {
                output += "private ";
            }
            else if( IsProtected() )
            {
                output += "protected ";
            }
            return output;
        }

        public string FormatFlags()
        {
            ulong copyFlags = PropertyFlags;
            var output = string.Empty;
            var importantFlags = string.Empty;

            if( PropertyFlags == 0 )
            {
                output = FormatAccess();
                if (string.IsNullOrWhiteSpace(output) == false)
                    output = $"/*{output}*/";
                return output;
            }

            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.NeedCtorLink) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.NeedCtorLink;
            }

            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditorData) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditorData;
            }

            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Net) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Net;
            }

            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.New) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.New;
            }

            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.OnDemand) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.OnDemand;
            }

            // Decompiling of this flag is put elsewhere.
            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Editable) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Editable;
            }

            if( HasPropertyFlag( Flags.PropertyFlagsLO.Component ) )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Component;
            }

            if( Package.Version > 300 && (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Init) != 0 )
            {
                output += "init ";
            }

            /** Flags that are valid as parameters only */
            if( Outer is UFunction && (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Parm) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Parm;
                // Possible since UE3
                if( Package.Version > 300 )
                {
                    if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Const) != 0 )
                    {
                        output += "const ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Const;
                    }
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.CoerceParm) != 0 )
                {
                    output += "coerce ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.CoerceParm;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.OptionalParm) != 0 )
                {
                    output += "optional ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.OptionalParm;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.OutParm) != 0 )
                {
                    importantFlags += "ref "; // UnrealScript's out is a pass by reference
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.OutParm;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.SkipParm) != 0 )
                {
                    output += "skip ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.SkipParm;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.ReturnParm) != 0 )
                {
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.ReturnParm;
                }

                // Remove implied flags from GUIComponents
                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.ExportObject) != 0 )
                {
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.ExportObject;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditInline) != 0 )
                {
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditInline;
                }
            }
            else /** Not a function param. */
            {
                output += FormatAccess();

                // UE3 flags
                if( Package.Version > 129 )
                {
                    if( HasPropertyFlag( Flags.PropertyFlagsHO.PrivateWrite ) )
                    {
                        output += "privatewrite ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.PrivateWrite << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.ProtectedWrite ) )
                    {
                        output += "protectedwrite ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.ProtectedWrite << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.RepNotify ) )
                    {
                        output += "repnotify ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.RepNotify << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsLO.NoClear ))
                    {
                        output += "noclear ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.NoClear;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsLO.NoImport ) )
                    {
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.NoImport;
                        output += "noimport ";
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsLO.DataBinding ) )
                    {
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.DataBinding;
                        output += "databinding ";
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.EditHide ) )
                    {
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.EditHide << 32;
                        output += "edithide ";
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.EditTextBox ) )
                    {
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.EditTextBox << 32;
                        output += "edittextbox ";
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.Interp ) )
                    {
                        output += "interp ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.Interp << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.NonTransactional ) )
                    {
                        output += "nontransactional ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.NonTransactional << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsLO.DuplicateTransient ) )
                    {
                        output += "duplicatetransient ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.DuplicateTransient;

                        // Implies: Export, EditInline
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.EditorOnly ) )
                    {
                        output += "editoronly ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.EditorOnly << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.CrossLevelPassive ) )
                    {
                        output += "crosslevelpassive ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.CrossLevelPassive << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.CrossLevelActive ) )
                    {
                        output += "crosslevelactive ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.CrossLevelActive << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.Archetype ) )
                    {
                        output += "archetype ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.Archetype << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.NotForConsole ) )
                    {
                        output += "notforconsole ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.NotForConsole << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.RepRetry ) )
                    {
                        output += "repretry ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.RepRetry << 32;
                    }

                    // Instanced is only an alias for Export and EditInline.
                    /*if( HasPropertyFlag( Flags.PropertyFlagsLO.Instanced ) )
                    {
                        output += "instanced ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Instanced;

                        // Implies: Export, EditInline
                    }*/

                    if( Package.Version > 500 && HasPropertyFlag( Flags.PropertyFlagsLO.SerializeText ) )
                    {
                        output += "serializetext ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.SerializeText;
                    }
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Native) != 0 )
                {
                    output += FormatNative() + " ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Native;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Const) != 0 )
                {
                    output += "const ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Const;
                }

                if( Package.Version > 500 )
                {
                    if( HasPropertyFlag( Flags.PropertyFlagsLO.EditFixedSize ) )
                    {
                        output += "editfixedsize ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditFixedSize;
                    }
                }
                else
                {
                    if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditConstArray) != 0 )
                    {
                        output += "editconstarray ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditConstArray;
                    }
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditConst) != 0 )
                {
                    output += "editconst ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditConst;
                }

                // Properties flagged with automated, automatically get those flags added by the compiler.
                if( Package.Version == 128 && (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Automated) != 0 )
                {
                    output += "automated ";
                    copyFlags &= ~((ulong)Flags.PropertyFlagsLO.Automated
                        |(ulong)Flags.PropertyFlagsLO.EditInlineUse
                        |(ulong)Flags.PropertyFlagsLO.EditInlineNotify
                        |(ulong)Flags.PropertyFlagsLO.EditInline
                        |(ulong)Flags.PropertyFlagsLO.NoExport
                        |(ulong)Flags.PropertyFlagsLO.ExportObject);
                }
                else // Not Automated
                {
                    if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.NoExport) != 0 )
                    {
                        output += "noexport ";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.NoExport;

                        if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.ExportObject) != 0)
                        {
                            copyFlags &= ~(ulong)Flags.PropertyFlagsLO.ExportObject;
                        }
                    }           // avoid outputing export when noexport is flagged as well!
                    else if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.ExportObject) != 0 )
                    {
                        if( !HasPropertyFlag( Flags.PropertyFlagsLO.DuplicateTransient ) )
                        {
                            output += "export ";
                        }
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.ExportObject;
                    }

                    if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditInline) != 0 )
                    {
                        if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditInlineUse) != 0 )
                        {
                            copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditInlineUse;
                            output += "editinlineuse ";
                        }
                        else if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditInlineNotify) != 0 )
                        {
                            copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditInlineNotify;
                            output += "editinlinenotify ";
                        }
                        else if( !HasPropertyFlag( Flags.PropertyFlagsLO.DuplicateTransient ) )
                        {
                            output += "editinline ";
                        }
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditInline;
                    }
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EdFindable) != 0 )
                {
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EdFindable;
                    output += "edfindable ";
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Deprecated) != 0 )
                {
                    output += "deprecated ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Deprecated;
                }

                // It is important to check for global before checking config! first
                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.GlobalConfig) != 0 )
                {
                    output += "globalconfig ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.GlobalConfig;
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Config;
                }
                else if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Config) != 0 )
                {
#if XCOM2
                    if ( ConfigName != null && !ConfigName.IsNone() )
                    {
                        output += "config(" + ConfigName.ToString() + ") ";
                    }
                    else
                    {
#endif
                        output += "config ";
#if XCOM2
                    }
#endif
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Config;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Localized) != 0 )
                {
                    output += "localized ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Localized;
                }

                if( Package.Version == 128 )
                {
                    if( HasPropertyFlag( Flags.PropertyFlagsLO.Cache ) )
                    {
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Cache;
                        output += "cache ";
                    }
                }

                if( HasPropertyFlag( Flags.PropertyFlagsLO.Transient ) )
                {
                    output += "transient ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Transient;
                }

                if( HasPropertyFlag( Flags.PropertyFlagsLO.Travel ) )
                {
                    output += "travel ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Travel;
                }

                if( HasPropertyFlag( Flags.PropertyFlagsLO.Input ) )
                {
                    output += "input ";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Input;
                }
            }
            // Local's may never output any of their implied flags!
            if( !IsParm() && Super != null
                && String.Compare( Super.GetClassName(), "Function", StringComparison.OrdinalIgnoreCase ) == 0 )
            {
                return string.Empty;
            }
            // alright...
            //return "/*" + UnrealMethods.FlagToString( PropertyFlags ) + "*/ " + output;
            if (string.IsNullOrWhiteSpace(output) == false)
                output = $"/*{output}*/";
            output += importantFlags;
            return copyFlags != 0 ? "/*" + UnrealMethods.FlagToString( copyFlags ) + "*/ " + output : output;
        }

        public IEnumerable<string> EnumerableFormatFlags()
        {
            ulong copyFlags = PropertyFlags;
            var output = string.Empty;

            // Local's may never output any of their implied flags!
            if( !IsParm() && Super != null
                          && String.Compare( Super.GetClassName(), "Function", StringComparison.OrdinalIgnoreCase ) == 0 )
            {
                yield break;
            }
            
            if( PropertyFlags == 0 )
            {
                yield break;
            }

            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.NeedCtorLink) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.NeedCtorLink;
            }

            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditorData) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditorData;
            }

            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Net) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Net;
            }

            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.New) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.New;
            }

            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.OnDemand) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.OnDemand;
            }

            // Decompiling of this flag is put elsewhere.
            if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Editable) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Editable;
            }

            if( HasPropertyFlag( Flags.PropertyFlagsLO.Component ) )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Component;
            }

            if( Package.Version > 300 && (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Init) != 0 )
            {
                yield return "init";
            }

            /** Flags that are valid as parameters only */
            if( Outer is UFunction && (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Parm) != 0 )
            {
                copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Parm;
                // Possible since UE3
                if( Package.Version > 300 )
                {
                    if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Const) != 0 )
                    {
                        yield return "const";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Const;
                    }
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.CoerceParm) != 0 )
                {
                    yield return "coerce";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.CoerceParm;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.OptionalParm) != 0 )
                {
                    yield return "optional";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.OptionalParm;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.OutParm) != 0 )
                {
                    yield return "ref"; // UnrealScript's out is a pass by reference
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.OutParm;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.SkipParm) != 0 )
                {
                    yield return "skip";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.SkipParm;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.ReturnParm) != 0 )
                {
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.ReturnParm;
                }

                // Remove implied flags from GUIComponents
                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.ExportObject) != 0 )
                {
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.ExportObject;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditInline) != 0 )
                {
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditInline;
                }
            }
            else /** Not a function param. */
            {
                // UE3 flags
                if( Package.Version > 129 )
                {
                    if( HasPropertyFlag( Flags.PropertyFlagsHO.PrivateWrite ) )
                    {
                        yield return "privatewrite";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.PrivateWrite << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.ProtectedWrite ) )
                    {
                        yield return "protectedwrite";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.ProtectedWrite << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.RepNotify ) )
                    {
                        yield return "repnotify";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.RepNotify << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsLO.NoClear ))
                    {
                        yield return "noclear";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.NoClear;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsLO.NoImport ) )
                    {
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.NoImport;
                        yield return "noimport";
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsLO.DataBinding ) )
                    {
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.DataBinding;
                        yield return "databinding";
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.EditHide ) )
                    {
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.EditHide << 32;
                        yield return "edithide";
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.EditTextBox ) )
                    {
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.EditTextBox << 32;
                        yield return "edittextbox";
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.Interp ) )
                    {
                        yield return "interp";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.Interp << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.NonTransactional ) )
                    {
                        yield return "nontransactional";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.NonTransactional << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsLO.DuplicateTransient ) )
                    {
                        yield return "duplicatetransient";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.DuplicateTransient;

                        // Implies: Export, EditInline
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.EditorOnly ) )
                    {
                        yield return "editoronly";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.EditorOnly << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.CrossLevelPassive ) )
                    {
                        yield return "crosslevelpassive";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.CrossLevelPassive << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.CrossLevelActive ) )
                    {
                        yield return "crosslevelactive";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.CrossLevelActive << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.Archetype ) )
                    {
                        yield return "archetype";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.Archetype << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.NotForConsole ) )
                    {
                        yield return "notforconsole";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.NotForConsole << 32;
                    }

                    if( HasPropertyFlag( Flags.PropertyFlagsHO.RepRetry ) )
                    {
                        yield return "repretry";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsHO.RepRetry << 32;
                    }

                    // Instanced is only an alias for Export and EditInline.
                    /*if( HasPropertyFlag( Flags.PropertyFlagsLO.Instanced ) )
                    {
                        yield return "instanced";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Instanced;

                        // Implies: Export, EditInline
                    }*/

                    if( Package.Version > 500 && HasPropertyFlag( Flags.PropertyFlagsLO.SerializeText ) )
                    {
                        yield return "serializetext";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.SerializeText;
                    }
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Native) != 0 )
                {
                    yield return FormatNative();
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Native;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Const) != 0 )
                {
                    yield return "const";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Const;
                }

                if( Package.Version > 500 )
                {
                    if( HasPropertyFlag( Flags.PropertyFlagsLO.EditFixedSize ) )
                    {
                        yield return "editfixedsize";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditFixedSize;
                    }
                }
                else
                {
                    if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditConstArray) != 0 )
                    {
                        yield return "editconstarray";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditConstArray;
                    }
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditConst) != 0 )
                {
                    yield return "editconst";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditConst;
                }

                // Properties flagged with automated, automatically get those flags added by the compiler.
                if( Package.Version == 128 && (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Automated) != 0 )
                {
                    yield return "automated";
                    copyFlags &= ~((ulong)Flags.PropertyFlagsLO.Automated
                        |(ulong)Flags.PropertyFlagsLO.EditInlineUse
                        |(ulong)Flags.PropertyFlagsLO.EditInlineNotify
                        |(ulong)Flags.PropertyFlagsLO.EditInline
                        |(ulong)Flags.PropertyFlagsLO.NoExport
                        |(ulong)Flags.PropertyFlagsLO.ExportObject);
                }
                else // Not Automated
                {
                    if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.NoExport) != 0 )
                    {
                        yield return "noexport";
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.NoExport;

                        if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.ExportObject) != 0)
                        {
                            copyFlags &= ~(ulong)Flags.PropertyFlagsLO.ExportObject;
                        }
                    }           // avoid outputing export when noexport is flagged as well!
                    else if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.ExportObject) != 0 )
                    {
                        if( !HasPropertyFlag( Flags.PropertyFlagsLO.DuplicateTransient ) )
                        {
                            yield return "export";
                        }
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.ExportObject;
                    }

                    if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditInline) != 0 )
                    {
                        if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditInlineUse) != 0 )
                        {
                            copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditInlineUse;
                            yield return "editinlineuse";
                        }
                        else if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EditInlineNotify) != 0 )
                        {
                            copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditInlineNotify;
                            yield return "editinlinenotify";
                        }
                        else if( !HasPropertyFlag( Flags.PropertyFlagsLO.DuplicateTransient ) )
                        {
                            yield return "editinline";
                        }
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EditInline;
                    }
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.EdFindable) != 0 )
                {
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.EdFindable;
                    yield return "edfindable";
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Deprecated) != 0 )
                {
                    yield return "deprecated";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Deprecated;
                }

                // It is important to check for global before checking config! first
                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.GlobalConfig) != 0 )
                {
                    yield return "globalconfig";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.GlobalConfig;
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Config;
                }
                else if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Config) != 0 )
                {
#if XCOM2
                    if ( ConfigName != null && !ConfigName.IsNone() )
                    {
                        yield return "config(" + ConfigName.ToString() + ")";
                    }
                    else
                    {
#endif
                        yield return "config";
#if XCOM2
                    }
#endif
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Config;
                }

                if( (PropertyFlags & (ulong)Flags.PropertyFlagsLO.Localized) != 0 )
                {
                    yield return "localized";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Localized;
                }

                if( Package.Version == 128 )
                {
                    if( HasPropertyFlag( Flags.PropertyFlagsLO.Cache ) )
                    {
                        copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Cache;
                        yield return "cache";
                    }
                }

                if( HasPropertyFlag( Flags.PropertyFlagsLO.Transient ) )
                {
                    yield return "transient";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Transient;
                }

                if( HasPropertyFlag( Flags.PropertyFlagsLO.Travel ) )
                {
                    yield return "travel";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Travel;
                }

                if( HasPropertyFlag( Flags.PropertyFlagsLO.Input ) )
                {
                    yield return "input";
                    copyFlags &= ~(ulong)Flags.PropertyFlagsLO.Input;
                }
            }
            
            if(copyFlags != 0)
                yield return UnrealMethods.FlagToString( copyFlags );
        }
    }
}
#endif