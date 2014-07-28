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
    let tmp = "$nuget_create"
    let srcDir = dir projName

    logfn "Source directory: %s" srcDir

    let dllsWithPath = dllsToCopy |> Seq.map(fun s -> srcDir @@ s)
    let mainDllWithPath = srcDir @@ mainDll 

    tryDelDir tmp
    ensureDirectory (tmp @@ "lib/net45")
    CopyFiles (tmp @@ "lib/net45") dllsWithPath
    NuGet (fun p ->
            { p with
                OutputPath = "./"
                WorkingDir = tmp
                Version = getVersion(mainDllWithPath)
                Dependencies = dependencies
            }) nuspecFile
    
    DeleteDir tmp

Target "Nuget" (fun _ ->
    let stacksVer = getVersion(dir("Stacks") @@ "Stacks.dll")

    //Main stacks package
    BuildPackage "Stacks" 
                 ["Stacks.dll"] 
                 "Stacks.dll"  
                 [ ("Microsoft.Tpl.Dataflow", "4.5.14" );
                   ("Rx-Main", "2.2.4");
                   ("protobuf-net", "2.0.0.668") ]
                 "Stacks/stacks.nuspec"

    BuildPackage "Stacks.MessagePack"
                 ["Stacks.MessagePack.dll"]
                 "Stacks.MessagePack.dll"
                 [ ("Stacks", stacksVer )
                   ("MsgPack.Cli", "0.4.3") ]
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


