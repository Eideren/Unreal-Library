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
            "^AnimSequence$",
            "AnimNode",
            "AnimSet",
            "AnimNotify",
            "SkelControl",
            
            "^TdSwanNeck$",
            "^TdMove$",
            "TdMove_",
            "TdMOVE_",
            "TdMoveManager",
            "TdPlayerMoveManager",
            "TdPhysicsMove",
            "^TdPlayerController$",
            "^Interaction$",
            "^Input$",
            "^PlayerInput$",
            "^TdPlayerInput$",
            "^TdLookAtPoint$",
            "^SavedMove$",
            "^TdSavedMove$",
            "^TdMovementSplineMarker$",
            
            "^SkeletalMeshComponent$",
            "^TdSkeletalMeshComponent$",
            "^Camera$",
            "^TdCamera$",
            
            "^Td.*Volume$",
            "^TdTrigger$",
            "^PhysicsVolume$",
            "^TriggerVolume$",
            "^Volume$",
            "^Brush$",
            
            "^InterpCurveFloat$",
            "^CylinderComponent$",
            "^Scene$",
            
            "^Actor$",
            "^KActor$",
            "^DynamicSMActor$",
            
            "^Pawn$",
            "^TdPawn$",
            "^TdPlayerPawn$",
            
            "^TdController$",
            "^PlayerController$",
            "^GamePlayerController$",
            
            "^Camera$",
            "^CameraActor$",
            "^TdPlayerCamera$",
        };
        public static string[] filtersOut => new string[]
        {
            "Commandlet",
            "SeqVar",
            "SeqAct",
            "TdAI",
            "TdUI",
            "UIData",
            "Factory",
            "InterpTrack",
            "AnimNodeEditInfo_AimOffset",
            "TdMove_PursuitMelee",
            "TdMove_.*Cover",
            "TdMove_HeadButtedByCeleste",
            "TdMove_Disarmed_Boss",
            "TdMove_JumpBot_",
            "TdMove_Melee_Assault",
            "TdMove_Melee_",
            "^TdMove_MeleeDummy$",
            "^TdMove_Stumble_",
            "TdCoverGroupVolume",
            "TdAnimNodeCover",
            "TdAnimNodeAiAnimationState",
            "TdAnimNodeAimNodeState",
            "AI([A-Z]|$)",
            "Ai([A-Z]|$)",
            "Bot([A-QS-Z]|$)", // we need TdMove_BotRoll, so 'R' is filtered out
            "Tutorial"
        };

        static IEnumerable<T> EnumerateInheritance<T>(T o) where T : UField
        {
            for (T supers = o; supers != null; supers = supers.Super as T)
            {
                yield return supers;
            }
        }

        static IEnumerable<UObject> EnumerateObjectAndImportExport(UnrealPackage o)
        {
            foreach (var obj in o.Objects)
                yield return obj;
            
            foreach (var obj in o.Exports)
                yield return obj.Object;
            
            foreach (var obj in o.Imports)
                yield return obj.Object;
        }


        static bool InheritanceMatch(UImportTableItem import, UExportTableItem export)
        {
            UObjectTableItem e, i;
            for (i = import, e = export; true; i = i.OuterTable, e = e.OuterTable)
            {
                if (i == null && e == null)
                    return true;
                if (i != null && e == null && i.ClassName == "Package")
                    return true; // This is fine, export just doesn't include package as the outer for class
                if (i == null || e == null)
                    return false;
                if (i.ClassName != e.ClassName || i.ObjectName != e.ObjectName)
                    return false;
            }
        }


        static void FixImports(UnrealPackage package, IEnumerable<UnrealPackage> oldPackages)
        {
            foreach (var tableItem in package.Imports)
            {
                UObject result = null;
                if (tableItem.ClassName == "Package")
                    continue;

                foreach (var o in 
                    from p in oldPackages
                    from o in p.Exports
                    where (o.Object is UnknownObject) == false
                          && InheritanceMatch(tableItem, o)
                    select o)
                {
                    if (result != null)
                        Debugger.Break();
                    else
                        result = o.Object;
                    
                    if (tableItem.PackageName == o.Object.Package.PackageName)
                        break;
                }

                if (result != null)
                    tableItem.Object = result;
            }
        }


        static void Main(string[] args)
        {
            goto REAL;
            UnrealConfig.SuppressComments = false;
            var nnn = new NativesTablePackage();
            nnn.LoadPackage( @"C:\Program Files (x86)\Eliot\UE Explorer\Native Tables\NativesTableList_UT3" );
            var pacpapcpapc = UnrealLoader.LoadPackage(@"C:\UDK\UDK-2014-02\UDKGame\Script\UTEditor.u");
            pacpapcpapc.NTLPackage = nnn;
            pacpapcpapc.InitializePackage();
            var stuff = pacpapcpapc.Objects.FirstOrDefault(x => x is UClass && x.Name == "UTUnrealEdEngine");
            var v = stuff.Decompile();
            var tokens = (from x in (stuff as UClass).Functions select (x.Name, x.ByteCodeManager.DeserializedTokens)).ToArray();
            
            
            return;
            {
            }
            REAL:
            // Order matters here, from least to most derived
            var paths = new []
            {
                @"D:\MirrorsEdge\Tools\unpacked\Core.u",
                @"D:\MirrorsEdge\Tools\unpacked\Engine.u",
                @"D:\MirrorsEdge\Tools\unpacked\Editor.u",
                @"D:\MirrorsEdge\Tools\unpacked\UnrealEd.u",
                
                @"D:\MirrorsEdge\Tools\unpacked\Fp.u",
                @"D:\MirrorsEdge\Tools\unpacked\Tp.u",
                @"D:\MirrorsEdge\Tools\unpacked\Ts.u",
                @"D:\MirrorsEdge\Tools\unpacked\IpDrv.u",
                @"D:\MirrorsEdge\Tools\unpacked\GameFramework.u",
                
                @"D:\MirrorsEdge\Tools\unpacked\TdGame.u",
                @"D:\MirrorsEdge\Tools\unpacked\TdMenuContent.u",
                @"D:\MirrorsEdge\Tools\unpacked\TdMpContent.u",
                @"D:\MirrorsEdge\Tools\unpacked\TdSharedContent.u",
                @"D:\MirrorsEdge\Tools\unpacked\TdSpBossContent.u",
                @"D:\MirrorsEdge\Tools\unpacked\TdSpContent.u",
                @"D:\MirrorsEdge\Tools\unpacked\TdTTContent.u",
                @"D:\MirrorsEdge\Tools\unpacked\TdTuContent.u",
                @"D:\MirrorsEdge\Tools\unpacked\TdEditor.u",
            };
            
            
            
            
            
            UnrealConfig.StubMode = false;
            var destFolder = @"D:\MirrorsEdge\Sources\MEdgeSharp\AnimationSystem\Converted";
            if(UnrealConfig.StubMode)
                destFolder = @"D:\MirrorsEdge\Sources\MEdgeSharp\AnimationSystem\Stubs";
            
            var packages = new List<(UnrealPackage package, string path)>(); // List to keep the right order
            var NTL = new NativesTablePackage();
            NTL.LoadPackage( @"C:\Program Files (x86)\Eliot\UE Explorer\Native Tables\NativesTableList_UT3" );
            WriteLine("\tLoading packages");
            foreach (string pFile in paths)
            {
                var package = UnrealLoader.LoadPackage(pFile);
                package.NTLPackage = NTL;
                packages.Add((package, pFile));
            }

            WriteLine("\tFixing imports");
            foreach (var (package, path) in packages)
            {
                WriteLine($"\t\t{package.PackageName}");
                package.InitializePackage(UnrealPackage.InitFlags.RegisterClasses );
                package.InitializeExportObjects();
            }
            foreach (var (package, path) in packages)
            {
                WriteLine($"\t\t{package.PackageName}");
                FixImports(package, from p in packages select p.package);
            }

            WriteLine("\tInitializing packages");
            foreach (var (package, path) in packages)
            {
                WriteLine($"\t\t{package.PackageName}");
                package.InitializePackage(UnrealPackage.InitFlags.All & ~UnrealPackage.InitFlags.RegisterClasses);
            }
            
            UnrealConfig.SharedPackages.Clear();
            foreach (var kvp in packages)
                UnrealConfig.SharedPackages.Add(kvp.package);

            // Mark functions overriden by states as such to generate delegate variable for those functions in the decompilation process
            WriteLine("\tMarking state-overriden functions");
            foreach (var refFunction in from p in packages
                from o in EnumerateObjectAndImportExport(p.package)
                where o is UState && (o is UClass == false)
                from function in (o as UState).Functions
                select function)
            {
                foreach (var f in EnumerateInheritance(refFunction))
                {
                    if (f.SelfOverridenByState)
                        continue;
                    
                    f.SelfOverridenByState = true;
                    
                    foreach (var sameFuncInOtherPackages in 
                        from package in packages
                        from obj in EnumerateObjectAndImportExport(package.package)
                        where obj is UState && obj.Name == f.Outer.Name // pick objects whose name matches the class name of the function
                        from func in (obj as UState).Functions
                        where func.Name == f.Name// pick functions matching the full definition of the reference one, ignore those already processed
                        select func) // Find any function matching the refFunction's description in other packages
                    {
                        sameFuncInOtherPackages.SelfOverridenByState = true;
                    }
                }
            }
            
            List<UClass> selectedClasses = new List<UClass>();
            foreach (var c in from p in packages
                from o in p.package.Objects
                where o is UClass c 
                      && c.Outer is UClass == false
                      && (c.Outer == null || (c.Outer is UPackage p2 && p2.Name == c.Package.PackageName))
                select o as UClass)
            {
                bool add = Filtering(c);
                if (UnrealConfig.StubMode)
                    add = !add;
                if (add)
                {
                    var match = (from x in selectedClasses where x.Name == c.Name select x).FirstOrDefault();
                    if (ReferenceEquals(match, c))
                        continue; // Already included
                    
                    if(match != null)
                        System.Diagnostics.Debugger.Break(); // Duplicate different class !?
                    
                    selectedClasses.Add(c); 
                }
            }
            
            bool Filtering(UClass c)
            {
                string n = c.Name;
                foreach (var x in filtersOut)
                {
                    if (Regex.Match(n, x) is var m && m.Success)
                        return false;
                }

                foreach (var x in filters)
                {
                    if (Regex.Match(n, x) is var m && m.Success)
                        return true;
                }
                    
                return false;
            }

            HashSet<string> packageName = new HashSet<string>();
            foreach (var p in packages)
                packageName.Add(p.package.PackageName);
            
            while (true)
            {
                UnrealConfig.SuppressComments = false;

                foreach (UClass c in selectedClasses)
                {
                    var outPath = @$"{destFolder}\{c.Package.PackageName}\{c.Name}.cs";

                    if (File.Exists(outPath) && (from x in File.ReadLines(outPath) where x.Contains("NO OVERWRITE") || x.Contains("NO OVERRIDE") select x).FirstOrDefault() != null)
                    {
                        WriteLine("SKIP\t"+c.Name);
                        continue;
                    }

                    WriteLine("\t"+c.Name);

                    var decompilation = c.Decompile();
                    decompilation = decompilation.Replace("\r\n", "\r\n\t");
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    var usings = string.Join(" ", from i in packageName where i != c.Package.PackageName select $"using {i};");
                    File.WriteAllText(outPath, $"namespace MEdge.{c.Package.PackageName}{{\n{usings}\n\n{decompilation}\n}}");
                }
                
                WriteLine("--DONE--");

                if (Debugger.IsAttached)
                    Debugger.Break();
                else
                    break;
            }
        }
    }
}
