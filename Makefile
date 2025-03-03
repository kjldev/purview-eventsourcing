include .build/common.mk

# Variables
ROOT_FOLDER = src/
SOLUTION_FILE = $(ROOT_FOLDER)Purview.EventSourcing.slnx
TEST_PROJECT = $(ROOT_FOLDER)Purview.EventSourcing.slnx
CONFIGURATION = Release

PACK_VERSION = 1.1.0
ARTIFACT_FOLDER = p:/sync-projects/.local-nuget/

# Targets
vs: ## Open the solution in Visual Studio
	@start "$(SOLUTION_FILE)"

build: ## Build the solution
	dotnet build $(SOLUTION_FILE) --configuration $(CONFIGURATION)

test: ## Run the tests
	dotnet test $(TEST_PROJECT) --configuration $(CONFIGURATION)

pack: ## Pack the solution
	dotnet pack -c $(CONFIGURATION) -o $(ARTIFACT_FOLDER) $(SOLUTION_FILE) --property:Version=$(PACK_VERSION) --include-symbols

format: ## Format the code
	dotnet format $(ROOT_FOLDER)

act: ## Run act
	act -P ubuntu-latest=-self-hosted
