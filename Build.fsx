#r "tools/FAKE/tools/FakeLib.dll"

open Fake

let configuration = getBuildParamOrDefault "configuration" "Release"
let platform = getBuildParamOrDefault "buildPlatform" "Any CPU"

let dir (project) = 
    project @@ "bin" @@ (if platform = "Any CPU" then "" else platform) @@ configuration

let getVersion file = 
    System.Reflection.AssemblyName.GetAssemblyName(file).Version.ToString()

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

    CleanDir tmp
    ensureDirectory (tmp @@ "lib/net451")
    CopyFiles (tmp @@ "lib/net451") dllsWithPath
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
                 [ ("NLog", "2.1.0");
                   ("Microsoft.Tpl.Dataflow", "4.5.14" )]
                 "Stacks/stacks.nuspec"

    BuildPackage "Stacks.ProtoBuf"
                 ["Stacks.ProtoBuf.dll"]
                 "Stacks.ProtoBuf.dll"
                 [ ("Stacks", stacksVer )
                   ("protobuf-net", "2.0.0.668") ]
                 "Stacks.ProtoBuf/stacks.protobuf.nuspec"

    BuildPackage "Stacks.MessagePack"
                 ["Stacks.MessagePack.dll"]
                 "Stacks.MessagePack.dll"
                 [ ("Stacks", stacksVer )
                   ("MsgPack.Cli", "0.4.3") ]
                 "Stacks.MessagePack/stacks.messagepack.nuspec"
)

Target "All" DoNothing

"All" <== [ "Build"; "Nuget" ]

RunTargetOrDefault "Build"


