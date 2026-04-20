set quiet := true
set windows-shell := ["pwsh", "-NoProfile", "-Command"]

# Variables

root_folder := "src"
solution_file := root_folder + "/Purview.EventSourcing.slnx"
test_root := root_folder + "/tests"
package_manifest := "package.json"
configuration := "Release"
artifact_folder := "artifacts/packages"
event_sourcing_package_project := root_folder + "/src/EventSourcing/EventSourcing.csproj"
event_sourcing_shared_package_project := root_folder + "/src/EventSourcing.Shared/EventSourcing.Shared.csproj"
event_sourcing_azure_storage_package_project := root_folder + "/src/EventSourcing.AzureStorage/EventSourcing.AzureStorage.csproj"
event_sourcing_cosmos_db_snapshot_package_project := root_folder + "/src/EventSourcing.CosmosDb.Snapshot/EventSourcing.CosmosDb.Snapshot.csproj"
event_sourcing_mongo_db_events_package_project := root_folder + "/src/EventSourcing.MongoDb.Events/EventSourcing.MongoDB.Events.csproj"
event_sourcing_mongo_db_snapshot_package_project := root_folder + "/src/EventSourcing.MongoDb.Snapshot/EventSourcing.MongoDB.Snapshot.csproj"
event_sourcing_source_generator_package_project := root_folder + "/src/EventSourcing.SourceGenerator/EventSourcing.SourceGenerator.csproj"
event_sourcing_sql_server_events_package_project := root_folder + "/src/EventSourcing.SqlServer.Events/EventSourcing.SqlServer.Events.csproj"
event_sourcing_sql_server_snapshot_package_project := root_folder + "/src/EventSourcing.SqlServer.Snapshot/EventSourcing.SqlServer.Snapshot.csproj"
event_sourcing_integration_tests_project := test_root + "/EventSourcing.IntegrationTests/EventSourcing.IntegrationTests.csproj"
event_sourcing_samples_integration_tests_project := test_root + "/EventSourcing.Samples.IntegrationTests/EventSourcing.Samples.IntegrationTests.csproj"
event_sourcing_samples_unit_tests_project := test_root + "/EventSourcing.Samples.UnitTests/EventSourcing.Samples.UnitTests.csproj"
event_sourcing_samples_web_integration_tests_project := test_root + "/EventSourcing.Samples.Web.IntegrationTests/EventSourcing.Samples.Web.IntegrationTests.csproj"
event_sourcing_source_generator_unit_tests_project := test_root + "/EventSourcing.SourceGenerator.UnitTests/EventSourcing.SourceGenerator.UnitTests.csproj"
event_sourcing_unit_tests_project := test_root + "/EventSourcing.UnitTests/EventSourcing.UnitTests.csproj"

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

# Print the current package version from package.json
version:
    node -p "require('./{{ package_manifest }}').version"

# Preview the next semantic version using commit-and-tag-version
version-next:
    npx commit-and-tag-version --dry-run

# Create the local release commit and changelog update without creating a tag
version-bump:
    npm run release

# Run all executable test projects under src/tests, excluding SharedTestingFramework
test:
    @just test-project {{ event_sourcing_integration_tests_project }}
    @just test-project {{ event_sourcing_samples_integration_tests_project }}
    @just test-project {{ event_sourcing_samples_unit_tests_project }}
    @just test-project {{ event_sourcing_samples_web_integration_tests_project }}
    @just test-project {{ event_sourcing_source_generator_unit_tests_project }}
    @just test-project {{ event_sourcing_unit_tests_project }}

# Run tests with a TUnit treenode filter (e.g.: just test-filter "/*/*/*/MyTest*")
test-filter filter:
    @just test-project-filter {{ event_sourcing_integration_tests_project }} "{{ filter }}"
    @just test-project-filter {{ event_sourcing_samples_integration_tests_project }} "{{ filter }}"
    @just test-project-filter {{ event_sourcing_samples_unit_tests_project }} "{{ filter }}"
    @just test-project-filter {{ event_sourcing_samples_web_integration_tests_project }} "{{ filter }}"
    @just test-project-filter {{ event_sourcing_source_generator_unit_tests_project }} "{{ filter }}"
    @just test-project-filter {{ event_sourcing_unit_tests_project }} "{{ filter }}"

# Run tests serially (useful for debugging)
test-serial:
    @just test-project-serial {{ event_sourcing_integration_tests_project }}
    @just test-project-serial {{ event_sourcing_samples_integration_tests_project }}
    @just test-project-serial {{ event_sourcing_samples_unit_tests_project }}
    @just test-project-serial {{ event_sourcing_samples_web_integration_tests_project }}
    @just test-project-serial {{ event_sourcing_source_generator_unit_tests_project }}
    @just test-project-serial {{ event_sourcing_unit_tests_project }}

[private]
test-project project:
    @echo "==> Testing {{ project }}"
    dotnet test --project {{ project }} --configuration {{ configuration }}

[private]
test-project-filter project filter:
    @echo "==> Testing {{ project }} with filter {{ filter }}"
    dotnet test --project {{ project }} --configuration {{ configuration }} -- --treenode-filter "{{ filter }}"

[private]
test-project-serial project:
    @echo "==> Testing {{ project }} serially"
    dotnet test --project {{ project }} --configuration {{ configuration }} -- --maximum-parallel-tests 1

# Pack all packable projects using the version from package.json
pack:
    node -e "const fs = require('node:fs'); const path = require('node:path'); const directory = path.resolve('{{ artifact_folder }}'); fs.mkdirSync(directory, { recursive: true }); for (const entry of fs.readdirSync(directory)) { if (entry.endsWith('.nupkg') || entry.endsWith('.snupkg')) { fs.rmSync(path.join(directory, entry), { force: true }); } }"
    @just pack-project {{ event_sourcing_azure_storage_package_project }}
    @just pack-project {{ event_sourcing_cosmos_db_snapshot_package_project }}
    @just pack-project {{ event_sourcing_mongo_db_events_package_project }}
    @just pack-project {{ event_sourcing_mongo_db_snapshot_package_project }}
    @just pack-project {{ event_sourcing_package_project }}
    @just pack-project {{ event_sourcing_shared_package_project }}
    @just pack-project {{ event_sourcing_source_generator_package_project }}
    @just pack-project {{ event_sourcing_sql_server_events_package_project }}
    @just pack-project {{ event_sourcing_sql_server_snapshot_package_project }}

[private]
pack-project project:
    @echo "==> Packing {{ project }} to {{ artifact_folder }}"
    dotnet pack "{{ project }}" --configuration "{{ configuration }}" --output "{{ artifact_folder }}"

# Publish packed NuGet packages to the specified source
# Optional environment variables:
# - NUGET_API_KEY: API key to pass to `dotnet nuget push`
# - NUGET_CONFIG_FILE: NuGet config file containing source credentials
publish nuget_source:
    node scripts/publish-packages.mjs "{{ artifact_folder }}" "{{ nuget_source }}"

# Format the code
format:
    dotnet tool restore
    dotnet csharpier format {{ root_folder }}

# Check formatting
check:
    dotnet tool restore
    dotnet csharpier check {{ root_folder }}
