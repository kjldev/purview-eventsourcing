set quiet := true

# Variables

root_folder := "src/"
solution_file := root_folder + "Purview.EventSourcing.slnx"
test_project := root_folder + "Purview.EventSourcing.slnf"
configuration := "Release"
pack_version := "1.1.0"
artifact_folder := "p:/sync-projects/.local-nuget/"

# Default recipe - list available recipes
default:
    @just --list

# Open the solution in Visual Studio
vs:
    @start "{{ solution_file }}"

# Build the solution
build:
    dotnet build {{ solution_file }} --configuration {{ configuration }}

# Run the tests
test:
    dotnet test --solution {{ test_project }} --configuration {{ configuration }}

# Pack the solution
pack:
    dotnet pack -c {{ configuration }} -o {{ artifact_folder }} {{ solution_file }} --property:Version={{ pack_version }} --include-symbols

# Format the code
format:
    dotnet format {{ root_folder }}

# Run act
act:
    act -P ubuntu-latest=-self-hosted
