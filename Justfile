set quiet := true
set windows-shell := ["pwsh", "-NoProfile", "-Command"]

# Variables

root_folder := "src"
solution_file := root_folder + "/Purview.EventSourcing.slnx"
test_root := root_folder + "/tests"
version_file := ".dotnet/Directory.Build.props"
configuration := "Release"
artifact_folder := "artifacts/packages"
tag_pattern := "v[0-9]*"

# Default recipe - list available recipes
[private]
default:
    just --list

# Open the solution in Visual Studio
vs:
    start "{{ solution_file }}"

# Build the solution
build:
    dotnet build {{ solution_file }} --configuration {{ configuration }}

# Restore local .NET tools
tools:
    dotnet tool restore

# Restore NuGet packages for the solution
restore:
    dotnet restore {{ solution_file }}

# Print the current package version from Directory.Build.props
version:
    & { [xml]$xml = Get-Content '{{ version_file }}'; $versionNode = @($xml.Project.PropertyGroup | Where-Object { $_.Version })[0]; if (-not $versionNode) { throw 'Version element not found in {{ version_file }}.' }; Write-Host $versionNode.Version }

# Calculate the next semantic version from the latest v* tag and conventional commits since that tag
version-next:
    & { $tag = git --no-pager tag --list '{{ tag_pattern }}' --sort=-version:refname | Select-Object -First 1; if (-not $tag) { $tag = 'v0.0.0'; $range = 'HEAD' } else { $range = "$tag..HEAD" }; $count = [int](git rev-list $range --count); $messages = if ($count -gt 0) { git --no-pager log $range --format=%B%n---END--- } else { '' }; $hasBreaking = $messages -match '(?m)^.+!:' -or $messages -match 'BREAKING CHANGE:'; $hasFeature = $messages -match '(?m)^feat(?:\(.+\))?: '; $match = [regex]::Match($tag, '^v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)'); if (-not $match.Success) { throw "Unable to parse semantic version from tag '$tag'." }; $major = [int]$match.Groups['major'].Value; $minor = [int]$match.Groups['minor'].Value; $patch = [int]$match.Groups['patch'].Value; if ($hasBreaking) { $major++; $minor = 0; $patch = 0 } elseif ($hasFeature) { $minor++; $patch = 0 } elseif ($count -gt 0) { $patch++ }; Write-Host "$major.$minor.$patch" }

# Update Directory.Build.props to the next semantic version
version-bump:
    & { $tag = git --no-pager tag --list '{{ tag_pattern }}' --sort=-version:refname | Select-Object -First 1; if (-not $tag) { $tag = 'v0.0.0'; $range = 'HEAD' } else { $range = "$tag..HEAD" }; $count = [int](git rev-list $range --count); $messages = if ($count -gt 0) { git --no-pager log $range --format=%B%n---END--- } else { '' }; $hasBreaking = $messages -match '(?m)^.+!:' -or $messages -match 'BREAKING CHANGE:'; $hasFeature = $messages -match '(?m)^feat(?:\(.+\))?: '; $match = [regex]::Match($tag, '^v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)'); if (-not $match.Success) { throw "Unable to parse semantic version from tag '$tag'." }; $major = [int]$match.Groups['major'].Value; $minor = [int]$match.Groups['minor'].Value; $patch = [int]$match.Groups['patch'].Value; if ($hasBreaking) { $major++; $minor = 0; $patch = 0 } elseif ($hasFeature) { $minor++; $patch = 0 } elseif ($count -gt 0) { $patch++ }; $nextVersion = "$major.$minor.$patch"; [xml]$xml = Get-Content '{{ version_file }}'; $versionNode = @($xml.Project.PropertyGroup | Where-Object { $_.Version })[0]; if (-not $versionNode) { throw 'Version element not found in {{ version_file }}.' }; if ($versionNode.Version -ne $nextVersion) { $versionNode.Version = $nextVersion; $resolvedVersionFile = (Resolve-Path '{{ version_file }}').Path; $xml.Save($resolvedVersionFile) }; Write-Host "Updated version to $nextVersion" }

# Run all executable test projects under src/tests, excluding SharedTestingFramework
test:
    & { $projects = @(Get-ChildItem -Path '{{ test_root }}' -Filter '*.csproj' -Recurse | Where-Object { $_.BaseName -ne 'SharedTestingFramework' } | Sort-Object FullName); if ($projects.Count -eq 0) { throw 'No test projects found.' }; foreach ($project in $projects) { Write-Host "==> Testing $($project.FullName)"; dotnet test --project $project.FullName --configuration '{{ configuration }}'; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } } }

# Run tests with a TUnit treenode filter (e.g.: just test-filter "/*/*/*/MyTest*")
test-filter filter:
    & { $projects = @(Get-ChildItem -Path '{{ test_root }}' -Filter '*.csproj' -Recurse | Where-Object { $_.BaseName -ne 'SharedTestingFramework' } | Sort-Object FullName); if ($projects.Count -eq 0) { throw 'No test projects found.' }; foreach ($project in $projects) { Write-Host "==> Testing $($project.FullName) with filter {{ filter }}"; dotnet test --project $project.FullName --configuration '{{ configuration }}' -- --treenode-filter '{{ filter }}'; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } } }

# Run tests serially (useful for debugging)
test-serial:
    & { $projects = @(Get-ChildItem -Path '{{ test_root }}' -Filter '*.csproj' -Recurse | Where-Object { $_.BaseName -ne 'SharedTestingFramework' } | Sort-Object FullName); if ($projects.Count -eq 0) { throw 'No test projects found.' }; foreach ($project in $projects) { Write-Host "==> Testing $($project.FullName) serially"; dotnet test --project $project.FullName --configuration '{{ configuration }}' -- --maximum-parallel-tests 1; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } } }

# Pack all packable projects using the current version from Directory.Build.props
pack:
    & { [xml]$xml = Get-Content '{{ version_file }}'; $versionNode = @($xml.Project.PropertyGroup | Where-Object { $_.Version })[0]; if (-not $versionNode) { throw 'Version element not found in {{ version_file }}.' }; $version = $versionNode.Version; $output = Join-Path (Get-Location) '{{ artifact_folder }}'; $projects = @(Get-ChildItem -Path '{{ root_folder }}' -Filter '*.csproj' -Recurse | Where-Object { Select-String -Path $_.FullName -Pattern '<IsPackable>true</IsPackable>' -Quiet } | Sort-Object FullName); if ($projects.Count -eq 0) { throw 'No packable projects found.' }; New-Item -ItemType Directory -Force -Path $output | Out-Null; Get-ChildItem -Path $output -Include '*.nupkg','*.snupkg' -File -ErrorAction SilentlyContinue | Remove-Item -Force; foreach ($project in $projects) { Write-Host "==> Packing $($project.FullName) as version $version to $output"; dotnet pack $project.FullName --configuration '{{ configuration }}' --output $output --property:Version=$version; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } } }

# Publish packed NuGet packages to the specified source
publish nuget_source api_key:
    & { $packages = @(Get-ChildItem -Path '{{ artifact_folder }}' -Filter '*.nupkg' | Sort-Object Name); if ($packages.Count -eq 0) { throw 'No packages found. Run `just pack` first.' }; foreach ($package in $packages) { Write-Host "==> Publishing $($package.Name) to {{ nuget_source }}"; dotnet nuget push $package.FullName --source '{{ nuget_source }}' --api-key '{{ api_key }}'; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } } }

# Format the code
format:
    dotnet tool restore
    dotnet csharpier format {{ root_folder }}

# Check formatting
check:
    dotnet tool restore
    dotnet csharpier check {{ root_folder }}
