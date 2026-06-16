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

event_sourcing_integration_tests_project := test_root + "/EventSourcing.IntegrationTests/EventSourcing.IntegrationTests.csproj"
event_sourcing_samples_integration_tests_project := test_root + "/EventSourcing.Samples.IntegrationTests/EventSourcing.Samples.IntegrationTests.csproj"
event_sourcing_samples_unit_tests_project := test_root + "/EventSourcing.Samples.UnitTests/EventSourcing.Samples.UnitTests.csproj"
event_sourcing_samples_web_integration_tests_project := test_root + "/EventSourcing.Samples.Web.IntegrationTests/EventSourcing.Samples.Web.IntegrationTests.csproj"
event_sourcing_source_generator_performance_tests_project := test_root + "/EventSourcing.SourceGenerator.PerformanceTests/EventSourcing.SourceGenerator.PerformanceTests.csproj"
event_sourcing_source_generator_unit_tests_project := test_root + "/EventSourcing.SourceGenerator.UnitTests/EventSourcing.SourceGenerator.UnitTests.csproj"
event_sourcing_unit_tests_project := test_root + "/EventSourcing.UnitTests/EventSourcing.UnitTests.csproj"

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
    @just test-project {{ event_sourcing_integration_tests_project }} {{ configuration }}
    @just test-project {{ event_sourcing_samples_integration_tests_project }} {{ configuration }}
    @just test-project {{ event_sourcing_samples_unit_tests_project }} {{ configuration }}
    @just test-project {{ event_sourcing_samples_web_integration_tests_project }} {{ configuration }}
    @just test-project {{ event_sourcing_source_generator_unit_tests_project }} {{ configuration }}
    @just test-project {{ event_sourcing_unit_tests_project }} {{ configuration }}

# Run tests with a TUnit treenode filter (e.g.: just test-filter "/*/*/*/MyTest*")
test-filter filter configuration=build_configuration:
    @just test-project-filter {{ event_sourcing_integration_tests_project }} "{{ filter }}" {{ configuration }}
    @just test-project-filter {{ event_sourcing_samples_integration_tests_project }} "{{ filter }}" {{ configuration }}
    @just test-project-filter {{ event_sourcing_samples_unit_tests_project }} "{{ filter }}" {{ configuration }}
    @just test-project-filter {{ event_sourcing_samples_web_integration_tests_project }} "{{ filter }}" {{ configuration }}
    @just test-project-filter {{ event_sourcing_source_generator_unit_tests_project }} "{{ filter }}" {{ configuration }}
    @just test-project-filter {{ event_sourcing_unit_tests_project }} "{{ filter }}" {{ configuration }}
    
# Run tests serially (useful for debugging)
test-serial configuration=build_configuration:
    @just test-project-serial {{ event_sourcing_integration_tests_project }} {{ configuration }}
    @just test-project-serial {{ event_sourcing_samples_integration_tests_project }} {{ configuration }}
    @just test-project-serial {{ event_sourcing_samples_unit_tests_project }} {{ configuration }}
    @just test-project-serial {{ event_sourcing_samples_web_integration_tests_project }} {{ configuration }}
    @just test-project-serial {{ event_sourcing_source_generator_unit_tests_project }}
    @just test-project-serial {{ event_sourcing_unit_tests_project }}

# Run source generator performance harness (pass --benchmark for larger runs)
perf-source-generator *args:
    dotnet run --project {{ event_sourcing_source_generator_performance_tests_project }} --configuration {{ build_configuration }} -- {{ args }}

[private]
test-project project configuration=build_configuration:
    @echo "==> Testing {{ project }}"
    dotnet test --project {{ project }} --configuration {{ configuration }}

[private]
test-project-filter project filter configuration=build_configuration    :
    @echo "==> Testing {{ project }} with filter {{ filter }}"
    dotnet test --project {{ project }} --configuration {{ configuration }} -- --treenode-filter "{{ filter }}"

[private]
test-project-serial project configuration=build_configuration:
    @echo "==> Testing {{ project }} serially"
    dotnet test --project {{ project }} --configuration {{ configuration }} -- --maximum-parallel-tests 1

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
