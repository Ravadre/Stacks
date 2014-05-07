#r "System.Xml.Linq.dll"

open System
open System.IO
open System.Xml.Linq
open System.Text.RegularExpressions

let xn (ns: XNamespace) x = XName.Get(x, ns.ToString())
let (@@) x y = Path.Combine(x, y)

let PatchSolution solutionFile = 
    let slnFile = FileInfo(solutionFile)
    let slnMonoFile = Path.GetDirectoryName(slnFile.FullName) @@ 
                        (Path.GetFileNameWithoutExtension(slnFile.Name) + "-Mono.sln")
    if File.Exists(slnMonoFile) then File.Delete(slnMonoFile)

    let projRegex = @"Project\(""\{[^}]+\}""\) = ""[^""]+"", ""[^""]+"", ""\{[^}]+\}"

    let outFile = File.CreateText(slnMonoFile)

    for line in File.ReadAllLines(slnFile.FullName) do
        match Regex.IsMatch(line, projRegex) with
        | true -> outFile.WriteLine(line.Replace(".csproj", "-Mono.csproj"))
        | false -> outFile.WriteLine(line)
    outFile.Close();

let PatchProject projectFile = 
    let projFileInfo = FileInfo(projectFile)
    let projMonoFile = Path.GetDirectoryName(projFileInfo.FullName) @@
                        (Path.GetFileNameWithoutExtension(projFileInfo.Name) + "-Mono.csproj")
    if File.Exists(projMonoFile) then 
        File.Delete(projMonoFile)

    let xml = XDocument.Load(projFileInfo.FullName)
    let xn = xn (xml.Root.GetDefaultNamespace())
    let axn x = XName.Get(x)
    
    //Changed target framework version to 4.5
    xml.Element(xn"Project").Elements(xn"PropertyGroup")
    |> Seq.iter(fun p ->
        match p.Element(xn"TargetFrameworkVersion") with
        | null -> ()
        | t -> t.Value <- "v4.5"
    )

    //Patch constants to add MONO (compilation requires it)
    xml.Element(xn"Project").Elements(xn"PropertyGroup")
    |> Seq.iter(fun p ->
        match p.Element(xn"DefineConstants") with
        | null -> ()
        | t -> match t.Value.Contains("MONO") with
               | true -> ()
               | false -> t.Value <- t.Value + ";MONO"
    )

    //Patch tools version to 4.0 (old MsBuild versioning)
    xml.Element(xn"Project").Attribute(axn"ToolsVersion").Value <- "4.0"

    xml.Save(projMonoFile)

let rec PatchAllProjects dir =
    Directory.EnumerateDirectories(dir)
    |> Seq.iter(fun d -> PatchAllProjects(d))
    
    Directory.EnumerateFiles(dir, "*.csproj")
    |> Seq.iter(fun f -> if not (f.Contains("-Mono")) then PatchProject(f))

PatchSolution "Stacks.sln"
PatchAllProjects "./"


