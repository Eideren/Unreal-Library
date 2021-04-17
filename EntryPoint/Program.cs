using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UELib;
using UELib.Core;
using static System.Console;

namespace EntryPoint
{
    class Program
    {
        public static string[] filters => new string[]
        {
            "AnimNode",
            "SkelControl",
            "^TdSwanNeck$",
            "^TdMove$",
            "TdMove_",
            "TdMoveManager",
            "TdPlayerMoveManager",
            "^TdPlayerController$",
            "TdPhysicsMove",
            "^TdLookAtPoint$",
            //"TdDamageType",
            "TdDmgType",
            "DamageType",
            "^Scene$",
            "^Actor$",
            "^Pawn$",
            "^TdPawn$",
            "^TdPlayerPawn$",
            "^PlayerController$",
            "^GamePlayerController$",
            "^ForceFeedbackWaveform$",
            "^SavedMove$"
        };
        public static string[] filtersOut => new string[]
        {
            "TdMove_Bot",
            "SeqAct",
            "SeqVar",
            "TdAI",
            "TdUI",
            "UIData",
            "Factory",
            "InterpTrack",
            "AnimNodeEditInfo_AimOffset",
            "TdMove_PursuitMelee"
        };
        
        
        static void Main(string[] args)
        {
            TextWriter dummy = new StringWriter();
            var outP = Out;
            //SetOut(dummy);
            
            
            
            var packages = new Dictionary<string, (UnrealPackage package, string path)>();
            var NTL = new NativesTablePackage();
            NTL.LoadPackage( @"C:\Program Files (x86)\Eliot\UE Explorer\Native Tables\NativesTableList_UT3" );
            foreach (string pFile in Directory.GetFiles(@"D:\MirrorsEdge\Tools\unpacked", "*.u", SearchOption.AllDirectories))
            {
                var package = UnrealLoader.LoadPackage(pFile);
                package.NTLPackage = NTL;
                packages.Add(package.PackageName, (package, pFile));
            }

            var classes = new Dictionary<string, UClass>();
            foreach (var (packageName, (package, path)) in packages)
            {
                WriteLine($"\t{package.PackageName}");
                
                // Fix all Imports, otherwise deserializer fails to properly deserialize data from classes from other packages 
                foreach (var tableItem in package.Imports)
                {
                    if (tableItem.ClassName == "Class" && classes.TryGetValue(tableItem.ObjectName, out var @class))
                        tableItem.Object = @class;
                }
                
                package.InitializePackage();
                foreach (UObject uObject in package.Objects)
                {
                    
                    if (uObject is UClass c)
                    {
                        classes.TryAdd(c.Name, c);
                    }
                }
            }

            var selectedClasses = classes.Select( x => x.Value )
                .Where(c =>
                {
                    foreach (var x in filtersOut)
                    {
                        if (Regex.Match(c.Name, x) is var m && m.Success)
                            return false;
                    }

                    foreach (var x in filters)
                    {
                        if (Regex.Match(c.Name, x) is var m && m.Success)
                            return true;
                    }
                    
                    return false;
                }).ToArray();
            
            while (true)
            {
                UnrealConfig.SuppressComments = true;
                foreach (UClass c in selectedClasses)
                {
                    outP.WriteLine(c.Name);
                    var outPath = @$"D:\MirrorsEdge\Sources\MEdgeSharp\AnimationSystem\Converted\{c.Package.PackageName}\{c.Name}.cs";
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    var decompiled = c.Decompile();
                    var content = $"namespace MEdge.{c.Package.PackageName}{{\nusing Core;using Engine;using GameFramework;\n{decompiled}\n}}";
                    File.WriteAllText(outPath, content);
                }

                if (Debugger.IsAttached)
                    Debugger.Break();
                else
                    break;
            }
        }
    }
}
