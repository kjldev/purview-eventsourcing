set quiet := true

# Variables
root_folder := "src"
test_root := root_folder + "/tests"

solution_file := root_folder + "/Purview.EventSourcing.slnx"

build_configuration := "Release"
artifacts_folder := "./artifacts"

current_version := `node -p "require('./package.json').version"`

event_sourcing_package_project := root_folder + "/src/EventSourcing/EventSourcing.csproj"
event_sourcing_azure_storage_project := root_folder + "/src/EventSourcing.AzureStorage/EventSourcing.AzureStorage.csproj"
event_sourcing_cosmosdb_project := root_folder + "/src/EventSourcing.CosmosDb/EventSourcing.CosmosDb.csproj"
event_sourcing_mongodb_project := root_folder + "/src/EventSourcing.MongoDB/EventSourcing.MongoDB.csproj"
event_sourcing_sql_server_project := root_folder + "/src/EventSourcing.SqlServer/EventSourcing.SqlServer.csproj"

event_sourcing_performance_tests := test_root + "/Purview.EventSourcing.PerformanceTests/Purview.EventSourcing.PerformanceTests.csproj"
event_sourcing_integration_tests := root_folder + "/Purview.EventSourcing.IntegrationTests.slnf"
event_sourcing_unit_tests := root_folder + "/Purview.EventSourcing.UnitTests.slnf"

# Default recipe - list available recipes
[private]
default:
    just --list

# Open the solution in Visual Studio
vs:
    open "{{ solution_file }}"

# Build the solution
build configuration=build_configuration version=current_version:
    dotnet build {{ solution_file }} -c {{configuration}} -p:Version={{version}}

# Restore local .NET tools
tools:
    dotnet tool restore

# Restore NuGet packages for the solution
restore:
    dotnet restore {{ solution_file }}

# Displays the current package version from package.json
current_version:
    @echo {{current_version}}

# Run all executable test projects under src/tests, excluding SharedTestingFramework
test configuration=build_configuration:
    @just test-project {{ event_sourcing_integration_tests }} {{ configuration }}
    @just test-project {{ event_sourcing_unit_tests }} {{ configuration }}

# Run source generator performance harness (pass --benchmark for larger runs)
perf-source-generator *args:
    dotnet run {{ event_sourcing_performance_tests }} --configuration {{ build_configuration }} -- {{ args }}

# Run SQL Server event/snapshot performance harness (pass --benchmark for larger runs)
perf-sql-server *args:
    dotnet run {{ event_sourcing_performance_tests }} --configuration {{ build_configuration }} -- {{ args }}

test-filter project filter="/*/*/*/*/" configuration=build_configuration:
    @echo "==> Testing {{ project }} with filter {{ filter }}"
    dotnet test {{ project }} --configuration {{ configuration }} --treenode-filter "{{ filter }}"

# Pack all packable projects using the version from package.json
pack publish_folder=artifacts_folder version=current_version configuration=build_configuration:
    @just pack-project {{ event_sourcing_package_project }} {{ version }} {{ configuration }} {{ publish_folder }}   
    @just pack-project {{ event_sourcing_azure_storage_project }} {{ version }} {{ configuration }} {{ publish_folder }}
    @just pack-project {{ event_sourcing_cosmosdb_project }} {{ version }} {{ configuration }} {{ publish_folder }}
    @just pack-project {{ event_sourcing_mongodb_project }} {{ version }} {{ configuration }} {{ publish_folder }}
    @just pack-project {{ event_sourcing_sql_server_project }} {{ version }} {{ configuration }} {{ publish_folder }}

[private]
pack-project project version=current_version configuration=build_configuration artifact_folder=artifacts_folder:
    @echo "==> Packing {{ project }} to {{ artifact_folder }}"
    dotnet pack "{{ project }}" --configuration "{{ configuration }}" --output "{{ artifact_folder }}" -p:PackageVersion="{{ version }}" -p:Version="{{ version }}"

# Format the code
lint-fix:
    dotnet csharpier format {{ root_folder }}

# Check formatting
lint-check:
    dotnet csharpier check {{ root_folder }}
