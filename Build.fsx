#r "tools/FAKE/tools/FakeLib.dll"

open Fake

let configuration = getBuildParamOrDefault "configuration" "Release"
let platform = getBuildParamOrDefault "buildPlatform" "Any CPU"
let betaVer = getBuildParamOrDefault "betaVer" ""

let dir (project) = 
    project @@ "bin" @@ (if platform = "Any CPU" then "" else platform) @@ configuration

let tryDelDir path = 
    try
        if System.IO.Directory.Exists(path) then
            System.IO.Directory.Delete(path, true)
    with
    | _ -> ()

let myEnsureDir path = 
    if not (System.IO.Directory.Exists(path)) then
        System.IO.Directory.CreateDirectory(path) |> ignore

let getVersion file = 
    let betaVer = if betaVer <> "" then "-" + betaVer else betaVer
    let v = System.Reflection.AssemblyName.GetAssemblyName(file).Version

    string(v.Major) + "." + string(v.Minor) + "." + string(v.Build) + betaVer


Target "Build" (fun _ ->
    RestorePackages()
    
    !! "Stacks.sln"
        |> MSBuild null "Build" [ ("Configuration", configuration); 
                                  ("Platform", platform) ]
        |> ignore
)

Target "Rebuild" (fun _ ->
    RestorePackages()

    !! "Stacks.sln"
        |> MSBuild null "Rebuild" [ ("Configuration", configuration); 
                                    ("Platform", platform) ]
        |> ignore
)

let BuildPackage projName dllsToCopy mainDll dependencies nuspecFile = 
    let tmp = ".nuget_create" + projName
    let srcDir = dir projName

    logfn "Source directory: %s" srcDir

    let dllsWithPath = dllsToCopy |> Seq.map(fun s -> srcDir @@ s)
    let mainDllWithPath = srcDir @@ mainDll 

    tryDelDir tmp
    myEnsureDir (tmp @@ "lib\\net45\\")
    CopyFiles (tmp @@ "lib\\net45\\") dllsWithPath

    NuGet (fun p ->
            { p with
                OutputPath = "./"
                WorkingDir = tmp
                Version = getVersion(mainDllWithPath)
                Dependencies = dependencies
            }) nuspecFile
    logfn "Built package %s" projName
        
    // For some readon this helps clearing directories on new FAKE
    System.Threading.Thread.Sleep(500)
    System.GC.Collect()
    tryDelDir tmp

Target "Nuget" (fun _ ->
    let stacksVer = getVersion(dir("Stacks") @@ "Stacks.dll")

    //Main stacks package
    BuildPackage "Stacks" 
                 ["Stacks.dll";
                  "Stacks.xml"] 
                 "Stacks.dll"  
                 [ ("Microsoft.Tpl.Dataflow", "4.5.24" );
                   ("Rx-Main", "2.2.5");
                   ("protobuf-net", "2.0.0.668") ]
                 "Stacks/stacks.nuspec"
    
    BuildPackage "Stacks.Actors" 
                 ["Stacks.Actors.dll";
                  "Stacks.Actors.xml"] 
                 "Stacks.Actors.dll"  
                 [ ("Stacks", stacksVer);
                   ("Microsoft.Tpl.Dataflow", "4.5.24" );
                   ("Rx-Main", "2.2.5");
                   ("protobuf-net", "2.0.0.668") ]
                 "Stacks.Actors/stacks.actors.nuspec"
    
    BuildPackage "Stacks.Actors.DI.Windsor"
                 ["Stacks.Actors.DI.Windsor.dll"]
                 "Stacks.Actors.DI.Windsor.dll"
                 [ ("Stacks.Actors", stacksVer );
                   ("Castle.Windsor", "3.3.0") ]
                 "Stacks.Actors.DI.Windsor/stacks.actors.di.windsor.nuspec"

    BuildPackage "Stacks.MessagePack"
                 ["Stacks.MessagePack.dll"]
                 "Stacks.MessagePack.dll"
                 [ ("Stacks", stacksVer )
                   ("MsgPack.Cli", "0.5.9") ]
                 "Stacks.MessagePack/stacks.messagepack.nuspec"

    BuildPackage "Stacks.FSharp"
                 [ "Stacks.FSharp.dll";
                   "Stacks.FSharp.xml" ]
                 "Stacks.FSharp.dll"
                 [ ("Stacks", stacksVer ) ]
                 "Stacks.FSharp/Stacks.FSharp.nuspec"
)

Target "All" DoNothing

"All" <== [ "Build"; "Nuget" ]

RunTargetOrDefault "Build"


