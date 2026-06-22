set quiet := true

# Variables
root_folder := "./src"
test_root := root_folder + "/tests"

solution_file := root_folder + "/Purview.EventSourcing.slnx"

sg_perf_tests := test_root + "/EventSourcing.SourceGenerator.PerformanceTests/EventSourcing.SourceGenerator.PerformanceTests.csproj"
sql_perf_tests := test_root + "/EventSourcing.SqlServer.PerformanceTests/EventSourcing.SqlServer.PerformanceTests.csproj"

build_configuration := "Release"
artifacts_folder := "./artifacts"

current_version := `node -p "require('./package.json').version"`

# Default recipe - list available recipes
[private]
default:
    just --list

# Open the solution in Visual Studio
vs:
    open "{{ solution_file }}"

# Build the solution
build project=solution_file configuration=build_configuration:
    echo "==> Building {{ BLUE }}{{ project }}{{ NORMAL }} ({{ GREEN }}{{ current_version }}{{ NORMAL }}) with configuration {{ YELLOW }}{{ configuration }}{{ NORMAL }}"
    dotnet build {{ project }} --configuration {{configuration}}

# Restore local .NET tools
tools:
    dotnet tool restore

# Restore NuGet packages for the solution
restore:
    dotnet restore {{ solution_file }}

# Displays the current package version from package.json
current_version:
    echo "==> Current version: {{ GREEN }}{{ current_version }}{{ NORMAL }} (defined in package.json and automatically included in the build output through the Purview.DotNetProjectSdk package)"

# Run source generator performance harness (pass --benchmark for larger runs)
perf-source-generator *args:
    dotnet run --project {{ sg_perf_tests }} --configuration {{ build_configuration }} -- {{ args }}

# Run SQL Server event/snapshot performance harness (pass --benchmark for larger runs)
perf-sql-server *args:
    dotnet run --project {{ sql_perf_tests }} --configuration {{ build_configuration }} -- {{ args }}

# Run tests for a specific project with a filter (e.g., "/*/*/*/*/") and configuration (e.g., "Release")
test project=solution_file filter="/*/*/*/*/" configuration=build_configuration:
    echo "==> Testing {{ BLUE }}{{ project }}{{ NORMAL }} ({{ GREEN }}{{ configuration }}{{ NORMAL }}) with filter {{ YELLOW }}{{ filter }}{{ NORMAL }}"
    dotnet test --project {{ project }} --configuration {{ configuration }} --treenode-filter "{{ filter }}" --ignore-exit-code 8

# Pack all packable projects
pack project=solution_file configuration=build_configuration artifact_folder=artifacts_folder:
    echo "==> Packing {{ BLUE }}{{ project }}{{ NORMAL }} ({{ GREEN }}{{ current_version }}{{ NORMAL }}) to {{ YELLOW }}{{ artifact_folder }}{{ NORMAL }}"
    dotnet pack "{{ project }}" --configuration "{{ configuration }}" --output "{{ artifact_folder }}"

# Format the code with CSharpier
lint-fix:
    dotnet csharpier format {{ root_folder }}

# Check formatting with CSharpier
lint-check:
    dotnet csharpier check {{ root_folder }}
