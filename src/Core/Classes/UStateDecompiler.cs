﻿#if DECOMPILE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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
            string content = FormatFunctionsOuter() + "\r\n" + FormatHeader() + UnrealConfig.PrintBeginBracket();
            UDecompilingState.AddTabs( 1 );

                var locals = FormatLocals();
                if( locals != String.Empty )
                {
                    content += "\r\n" + locals;
                }

                content += FormatFunctionScope();
                
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

            output = $"protected (System.Action<name>, StateFlow, System.Action<name>) {Name}()/*{output}*/";
            
            return output;
        }

        protected string FormatFunctionsOuter()
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
                    output += "\r\n" + UDecompilingState.Tabs + scriptFunction.Decompile() + "\r\n";
                }
                catch( Exception e )
                {
                    output += "\r\n" + UDecompilingState.Tabs + "// F:" + scriptFunction.Name + " E:" + e;
                }
            }

            if (GetType() != typeof(UState))
            {
                // Restore function state
                string restoreInstructions = "";
                UDecompilingState.AddTab();
                foreach (var scriptFunction in formatFunctions)
                {
                    if (scriptFunction.OverridenByState)
                    {
                        restoreInstructions += $"{UDecompilingState.Tabs}{scriptFunction.Name} = null;\r\n";
                    }
                }
                UDecompilingState.RemoveTab();

                if (restoreInstructions != "")
                {
                    output += $"{UDecompilingState.Tabs}protected override void RestoreDefaultFunction()\r\n{{\r\n{restoreInstructions}\r\n}}";
                }
            }

            return output;
        }

        string FormatFunctionScope()
        {
            if (Functions == null || !Functions.Any())
            {
                return String.Empty;
            }
            
            UDecompilingState.AddTab();
            string swapAndIgnore = "";
            var inheritance = new List<UState>();
            for (var curr = this; curr != null; curr = curr.Super != null && curr.Name == curr.Super.Name ? null : curr.Super as UState)
            {
                inheritance.Add(curr);
                string overrides = String.Empty;
                string ignores = String.Empty;
                foreach( var scriptFunction in curr.Functions )
                {
                    try
                    {
                        if (scriptFunction.Name == "BeginState" || scriptFunction.Name == "EndState")
                            continue;
                        if (scriptFunction.HasFunctionFlag(Flags.FunctionFlags.Defined))
                            overrides += $"{UDecompilingState.Tabs}{scriptFunction.Name} = {scriptFunction.NameOfSpecificFunctionImplementation};\r\n";
                        else if (curr._IgnoreMask != long.MaxValue)
                        {
                            var parameters = scriptFunction.FormatParms();
                            var paramsAsDiscard = string.Join("", (
                                from c in parameters
                                where c == ','
                                select "_"+c));
                            if (string.IsNullOrWhiteSpace(parameters) == false)
                                paramsAsDiscard += "_";
                            ignores += $" {scriptFunction.Name} = ({paramsAsDiscard})=>{{{(scriptFunction.ReturnType() != "void" ? "return default;" : "" )}}};";
                        }
                    }
                    catch( Exception e )
                    {
                        overrides += "\r\n" + UDecompilingState.Tabs + "// F:" + scriptFunction.Name + " E:" + e;
                    }
                }
                if(ignores != "")
                    ignores = $"{UDecompilingState.Tabs}/*ignores*/{ignores}\r\n\r\n";

                swapAndIgnore = $"{ignores}{overrides}\r\n{swapAndIgnore}";
                if(curr != this)
                    swapAndIgnore = $"\r\n{UDecompilingState.Tabs}// Inherited from {curr.Outer.Name}.{curr.Name}\r\n{swapAndIgnore}";
            }
            UDecompilingState.RemoveTab();
            
            
            
            var begins = from x in inheritance
                from f in x.Functions
                where f.HasFunctionFlag(Flags.FunctionFlags.Defined) && f.Name == "BeginState"
                select f;
            
            var ends = from x in inheritance
                from f in x.Functions
                where f.HasFunctionFlag(Flags.FunctionFlags.Defined) && f.Name == "EndState"
                select f;

            var beginScope = "";
            var beginName = "null";
            if (begins.Any())
            {
                if (begins.Count() == 1)
                {
                    beginName = begins.First().NameOfSpecificFunctionImplementation;
                }
                else
                {
                    beginName = "Begin";
                    foreach (var f in begins)
                        beginScope = $"{UDecompilingState.Tabs}{f.NameOfSpecificFunctionImplementation}(PreviousStateName);\r\n{beginScope}";
                    beginScope = $@"
{UDecompilingState.Tabs}void Begin(name PreviousStateName)
{UDecompilingState.Tabs}{{
{beginScope}{UDecompilingState.Tabs}}}
";
                }
            }

            var endScope = "";
            var endName = "null";
            if (ends.Any())
            {
                if (ends.Count() == 1)
                {
                    endName = ends.First().NameOfSpecificFunctionImplementation;
                }
                else
                {
                    endName = "End";
                    foreach (var f in ends)
                        endScope = $"{UDecompilingState.Tabs}{f.NameOfSpecificFunctionImplementation}(PreviousStateName);\r\n{endScope}";
                    endScope = $@"
{UDecompilingState.Tabs}void End(name PreviousStateName)
{UDecompilingState.Tabs}{{
{endScope}{UDecompilingState.Tabs}}}
";
                }
            }

            var labelScopes = new Dictionary<string, (string content, UState source)>();

            for (var s = this; s != null; s = s.Super as UState)
            {
                UDecompilingState.AddTab();
                var script = s.DecompileScript();
                UDecompilingState.RemoveTab();
                script = script.Replace("\r", "");
                var partialScope = "";
                var scopeLabel = "";
                foreach (var s1 in script.Split('\n'))
                {
                    if (Regex.Match(s1, @"^\w*:") is var m && m.Success)
                    {
                        if(scopeLabel != "" || partialScope != "")
                        {
                            if (scopeLabel == "")
                                scopeLabel = "Begin";
                            // parents should not override children labels, so we're going for a try add
                            labelScopes.TryAdd(scopeLabel, (partialScope, s));
                        }

                        scopeLabel = m.Value.TrimEnd(':');
                        partialScope = "";
                    }
                    else
                    {
                        if (partialScope != "")
                            partialScope += "\r\n";
                        partialScope += s1;
                    }
                }
                if(scopeLabel != "" || partialScope != "")
                {
                    if (scopeLabel == "")
                        scopeLabel = "Begin";
                    // parents should not override children labels, so we're going for a try add
                    labelScopes.TryAdd(scopeLabel, (partialScope, s));
                }
            }

            UDecompilingState.AddTab();
            var logic = "";
            foreach (var (label, scope) in labelScopes)
            {
                if(label == "Begin")
                    logic += $"{UDecompilingState.Tabs}if(jumpTo == null || jumpTo == \"Begin\")";
                else
                    logic += $"{UDecompilingState.Tabs}if(jumpTo == {label})";
                UDecompilingState.AddTab();
                logic += $"\r\n{UDecompilingState.Tabs}goto Begin;\r\n";
                UDecompilingState.RemoveTab();
            }

            foreach (var(label, scope) in labelScopes)
            {
                logic += $"\r\n{UDecompilingState.Tabs}{label}:{{}}";
                // Add comment to specify from which state we received this label when deriving
                if (scope.source != this)
                    logic += $"// {scope.source.Outer.Name}.{scope.source.Name}";
                logic += $"\r\n{scope.content}";
            }
            UDecompilingState.RemoveTab();

            if(labelScopes.Count > 1) // Not sure how to deal with multiple labels within a state, does the execution continue to the next label when at the end of this label's scope
                System.Diagnostics.Debugger.Break();
            
            return @$"{FormatConstants()}
{beginScope}
{UDecompilingState.Tabs}System.Collections.Generic.IEnumerable<Flow> StateFlow(name jumpTo = null)
{UDecompilingState.Tabs}{{
{swapAndIgnore}{logic}
{UDecompilingState.Tabs}}}
{endScope}
{UDecompilingState.Tabs}return ({beginName}, StateFlow, {endName});";
        }
    }
}
#endif