// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/build/FAKE/tools/FakeLib.dll"
#r @"packages/build/FSharp.Data/lib/net40/FSharp.Data.dll"
open Fake
open Fake.Git
open FSharp.Data
open FSharp.Data.JsonExtensions
open Fake.AssemblyInfoFile
open Fake.ReleaseNotesHelper
open Fake.UserInputHelper
open Fake.StringHelper
open Fake.Testing.XUnit2
open System
open System.IO
open System.Text
open System.Diagnostics
#if MONO
#else
#load "packages/build/SourceLink.Fake/tools/Fake.fsx"
open SourceLink
#endif

// Information about the project are used
//  - for version and project name in generated AssemblyInfo file
//  - by the generated NuGet package
//  - to run tests

// The name of the project
// (used by attributes in AssemblyInfo, name of a NuGet package and directory in 'src')
let project = "GoldenHammer"

// Short summary of the project
// (used as description in AssemblyInfo and as a short summary for NuGet package)
let summary = "Asset compilation pipeline"

// Longer description of the project
// (used as a description for NuGet package; line breaks are automatically cleaned up)
let description = "Asset build pipeline for preprocessing and packing game asset binaries."

// List of author names (for NuGet package)
let authors = [ "Tom Gillen" ]

// Tags for your project (for NuGet package)
let tags = "game build compile tools"

// File system information
let solutionFile  = "GoldenHammer.sln"

// Default target configuration
let configuration = "Release"

// Pattern specifying assemblies to be tested using NUnit
let testAssemblies = "tests/**/bin" </> configuration </> "*Tests*.dll"

// Git configuration (used for publishing documentation in gh-pages branch)
// The profile where the project is posted
let gitOwner = "TomGillen"
let gitHome = sprintf "%s/%s" "https://github.com" gitOwner

// The name of the project on GitHub
let gitName = "GoldenHammer"

// The url for the raw files hosted
let gitRaw = environVarOrDefault "gitRaw" "https://raw.githubusercontent.com/TomGillen"

// Output directories
let binOut = "build/bin"
let testOut = "build/test"
let packageOut = "build/package"

// --------------------------------------------------------------------------------------
// Versioning

type VersionInfo = { 
    SemVer : string;
    AssemblySemVer : string;
    NuGetVersion: string;
    InformationalVersion : string;
    PreReleaseTag: string; 
}
    
let readVersionJson json = {
    SemVer = json?SemVer.AsString()
    AssemblySemVer = json?AssemblySemVer.AsString()
    NuGetVersion = json?NuGetVersion.AsString()
    InformationalVersion = json?InformationalVersion.AsString()
    PreReleaseTag = json?PreReleaseTag.AsString() 
}

let getVersion() : VersionInfo =
    let output = new StringBuilder()
#if MONO
    let proc (info : ProcessStartInfo) = 
        info.FileName <- "mono"
        info.Arguments <- "packages/build/GitVersion.CommandLine/tools/GitVersion.exe"
#else
    let proc (info : ProcessStartInfo) = 
        info.FileName <- "packages/build/GitVersion.CommandLine/tools/GitVersion.exe"
#endif
    ExecProcessWithLambdas proc (TimeSpan.FromSeconds 5.0) true (fun error -> log error) (fun out -> output.Append(out) |> ignore) |> ignore
    output.ToString() |> trace
    output.ToString() |> JsonValue.Parse |> readVersionJson

let version = getVersion()

// Helper active pattern for project types
let (|Fsproj|Csproj|Vbproj|Shproj|) (projFileName:string) =
    match projFileName with
    | f when f.EndsWith("fsproj") -> Fsproj
    | f when f.EndsWith("csproj") -> Csproj
    | f when f.EndsWith("vbproj") -> Vbproj
    | f when f.EndsWith("shproj") -> Shproj
    | _                           -> failwith (sprintf "Project file %s not supported. Unknown project type." projFileName)

// Generate assembly info files with the right version & up-to-date information
Target "AssemblyInfo" (fun _ ->
    let getAssemblyInfoAttributes projectName =
        [ Attribute.Title (projectName)
          Attribute.Product project
          Attribute.Description summary
          Attribute.Version version.AssemblySemVer
          Attribute.FileVersion version.AssemblySemVer
          Attribute.Configuration configuration ]

    let getProjectDetails projectPath =
        let projectName = System.IO.Path.GetFileNameWithoutExtension(projectPath)
        ( projectPath,
          projectName,
          System.IO.Path.GetDirectoryName(projectPath),
          (getAssemblyInfoAttributes projectName)
        )

    !! "src/**/*.??proj"
    |> Seq.map getProjectDetails
    |> Seq.iter (fun (projFileName, projectName, folderName, attributes) ->
        match projFileName with
        | Fsproj -> CreateFSharpAssemblyInfo (folderName </> "AssemblyInfo.fs") attributes
        | Csproj -> CreateCSharpAssemblyInfo ((folderName </> "Properties") </> "AssemblyInfo.cs") attributes
        | Vbproj -> CreateVisualBasicAssemblyInfo ((folderName </> "My Project") </> "AssemblyInfo.vb") attributes
        | Shproj -> ()
        )
)

// --------------------------------------------------------------------------------------
// Collect binaries

// Copies binaries from default VS location to expected bin folder
// But keeps a subdirectory structure for each project in the
// src folder to support multiple project outputs
Target "CopyBinaries" (fun _ ->
    !! "src/**/*.??proj"
    -- "src/**/*.shproj"
    |>  Seq.map (fun f -> ((System.IO.Path.GetDirectoryName f) </> "bin" </> configuration, binOut </> (System.IO.Path.GetFileNameWithoutExtension f)))
    |>  Seq.iter (fun (fromDir, toDir) -> CopyDir toDir fromDir (fun _ -> true))
)

// --------------------------------------------------------------------------------------
// Clean build results

let vsProjProps = 
#if MONO
    [ ("DefineConstants","MONO"); ("Configuration", configuration) ]
#else
    [ ("Configuration", configuration); ("Platform", "Any CPU") ]
#endif

Target "Clean" (fun _ ->
    !! solutionFile |> MSBuildReleaseExt "" vsProjProps "Clean" |> ignore
    CleanDirs ["build"]
)

// --------------------------------------------------------------------------------------
// Build library & test project

Target "Build" (fun _ ->
    !! solutionFile
    |> MSBuildReleaseExt "" vsProjProps "Rebuild"
    |> ignore
)

// --------------------------------------------------------------------------------------
// Run the unit tests using test runner

Target "RunTests" (fun _ ->
    CreateDir testOut
    !! testAssemblies
    |> xUnit2 (fun p ->
        { p with
            HtmlOutputPath = Some( testOut </> "TestResults.html" );
            XmlOutputPath = Some( testOut </> "TestResults.xml" );
            TimeOut = TimeSpan.FromMinutes 20. })
)

#if MONO
#else
// --------------------------------------------------------------------------------------
// SourceLink allows Source Indexing on the PDB generated by the compiler, this allows
// the ability to step through the source code of external libraries http://ctaggart.github.io/SourceLink/

Target "SourceLink" (fun _ ->
    let baseUrl = sprintf "%s/%s/{0}/%%var2%%" gitRaw project
    !! "src/**/*.??proj"
    -- "src/**/*.shproj"
    |> Seq.iter (fun projFile ->
        let proj = VsProj.LoadRelease projFile
        SourceLink.Index proj.CompilesNotLinked proj.OutputFilePdb __SOURCE_DIRECTORY__ baseUrl
    )
)

#endif

// --------------------------------------------------------------------------------------
// Build a NuGet package

Target "NuGet" (fun _ ->
    Paket.Pack(fun p ->
        { p with
            OutputPath = packageOut
            Version = version.NuGetVersion
            ReleaseNotes = ""})
)

Target "PublishNuget" (fun _ ->
    Paket.Push(fun p ->
        { p with
            WorkingDir = packageOut })
)

Target "BuildPackage" DoNothing

// --------------------------------------------------------------------------------------
// Run all targets by default. Invoke 'build <Target>' to override

Target "All" DoNothing

"Clean"
  ==> "AssemblyInfo"
  ==> "Build"
  ==> "CopyBinaries"
  ==> "RunTests"
#if MONO
#else
  =?> ("SourceLink", Pdbstr.tryFind().IsSome )
#endif
  ==> "NuGet"
  ==> "BuildPackage"
  ==> "All"

"BuildPackage"
  ==> "PublishNuget"

RunTargetOrDefault "All"
