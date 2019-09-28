#r @"packages/build/FAKE/tools/FakeLib.dll"

open System
open Fake

Environment.CurrentDirectory <- __SOURCE_DIRECTORY__
let configuration = environVarOrDefault "Configuration" "Release"
let release = IO.File.ReadAllLines "RELEASE_NOTES.md" |> ReleaseNotesHelper.parseReleaseNotes
let description = "Functional ADO.NET for F#"
let tags = "F# sql"
let authors = "Mauricio Scheffer & irium"
let owners = "irium (formerly Mauricio Scheffer)"
let projectUrl = "https://github.com/irium/FsSql"
let licenceUrl = "http://www.apache.org/licenses/LICENSE-2.0"
let copyright = "Copyright Mauricio Scheffer \169 2011-2015 & irium \169 2018-2019"

let dotnetcliVersion = DotNetCli.GetDotNetSDKVersionFromGlobalJson()

let mutable dotnetExePath = "dotnet"

Target "InstallDotNetCore" (fun _ ->
    dotnetExePath <- DotNetCli.InstallDotNetSDK dotnetcliVersion
)

Target "Clean" (fun _ -> !!"./**/bin/" ++ "./**/obj/" |> CleanDirs)

open AssemblyInfoFile
Target "AssemblyInfo" (fun _ ->

    [ "FsSql"
    ]
    |> List.iter (fun product ->
        [ Attribute.Title product
          Attribute.Product product
          Attribute.Copyright copyright
          Attribute.Description description
          Attribute.Version release.AssemblyVersion
          Attribute.FileVersion release.AssemblyVersion
        ] |> CreateFSharpAssemblyInfo (product+"/AssemblyInfo.fs")
    )
)

// Target "PaketFiles" (fun _ ->
//     FileHelper.ReplaceInFiles ["namespace Logary.Facade","namespace Expecto.Logging"]
//         ["paket-files/logary/logary/src/Logary.Facade/Facade.fs"]
// )

Target "ProjectVersion" (fun _ ->
    [
        "FsSql/FsSql.fsproj"
    ]
    |> List.iter (fun file ->
        XMLHelper.XmlPoke file "Project/PropertyGroup/Version/text()" release.NugetVersion)
)

let build project =
    DotNetCli.Build (fun p ->
    { p with
        ToolPath = dotnetExePath
        Configuration = configuration
        Project = project
    })

Target "BuildTest" (fun _ ->
    build "FsSql.Tests/FsSql.Tests.fsproj"
)
let run f = DotNetCli.RunCommand (fun p -> { p with ToolPath = dotnetExePath } |> f)

Target "RunTest" (fun _ ->
    // run id ("FsSql.Tests/bin/"+configuration+"/netcoreapp2.0/FsSql.Tests.dll --summary")
    Shell.Exec ("FsSql.Tests/bin/"+configuration+"/net461/FsSql.Tests.exe","--summary")
    |> fun r -> if r<>0 then failwith "FsSql.Tests.exe failed"
)

Target "Pack" (fun _ ->
    let packParameters name =
        [
            "--no-build"
            "--no-restore"
            sprintf "/p:Title=\"%s\"" name
            "/p:PackageVersion=" + release.NugetVersion
            sprintf "/p:Authors=\"%s\"" authors
            sprintf "/p:Owners=\"%s\"" owners
            "/p:PackageRequireLicenseAcceptance=false"
            sprintf "/p:Description=\"%s\"" description
            sprintf "/p:PackageReleaseNotes=\"%O\"" ((toLines release.Notes).Replace(",",""))
            sprintf "/p:Copyright=\"%s\"" copyright
            sprintf "/p:PackageTags=\"%s\"" tags
            sprintf "/p:PackageProjectUrl=\"%s\"" projectUrl
            sprintf "/p:PackageLicenseUrl=\"%s\"" licenceUrl
        ] |> String.concat " "
    DotNetCli.RunCommand id
        ("pack FsSql/FsSql.fsproj -c "+configuration + " -o ../bin " + (packParameters "FsSql.Core"))
)

Target "Push" (fun _ -> Paket.Push (fun p -> { p with WorkingDir = "bin"; PublishUrl = "https://www.nuget.org" }))

#load "paket-files/build/fsharp/FAKE/modules/Octokit/Octokit.fsx"
Target "Release" (fun _ ->
    let gitOwner = "irium"
    let gitName = "FsSql"
    let gitOwnerName = gitOwner + "/" + gitName
    let remote =
        Git.CommandHelper.getGitResult "" "remote -v"
        |> Seq.tryFind (fun s -> s.EndsWith "(push)" && s.Contains gitOwnerName)
        |> function None -> ("ssh://github.com/"+gitOwnerName) | Some s -> s.Split().[0]

    Git.Staging.StageAll ""
    Git.Commit.Commit "" (sprintf "Bump version to %s" release.NugetVersion)
    Git.Branches.pushBranch "" remote (Git.Information.getBranchName "")

    Git.Branches.tag "" release.NugetVersion
    Git.Branches.pushTag "" remote release.NugetVersion

    let user = getUserInput "Github Username: "
    let pw = getUserPassword "Github Password: "

    Octokit.createClient user pw
    |> Octokit.createDraft gitOwner gitName release.NugetVersion
        (Option.isSome release.SemVer.PreRelease) release.Notes
    |> Octokit.releaseDraft
    |> Async.RunSynchronously
)

Target "All" ignore

"Clean"
==> "InstallDotNetCore"
==> "AssemblyInfo"
// ==> "PaketFiles"
==> "ProjectVersion"
==> "BuildTest"
==> "RunTest"
==> "Pack"
==> "All"
==> "Push"
==> "Release"

RunTargetOrDefault "All"
