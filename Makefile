PROJECT_NAME = GlamShuffle
CSPROJ = $(PROJECT_NAME).csproj
DLL_NAME = $(PROJECT_NAME).dll
JSON_NAME = $(PROJECT_NAME).json
YAML_NAME = $(PROJECT_NAME).yaml

BUILD_DIR = bin/Debug
BUILD_DLL = $(BUILD_DIR)/$(DLL_NAME)

PLUGIN_DIR = ~/Library/Application\ Support/XIV\ on\ Mac/dalamud/Hooks/dev/plugins
PLUGIN_DLL = $(PLUGIN_DIR)/$(DLL_NAME)
PLUGIN_JSON = $(PLUGIN_DIR)/$(JSON_NAME)

CONFIGURATION = Debug

.PHONY: all build clean deploy package help

all: build

build: $(PLUGIN_DIR)
	dotnet build -c $(CONFIGURATION)
	@test -f $(BUILD_DLL) || (echo "Build failed - DLL not found" && exit 1)
	@cp $(BUILD_DLL) $(PLUGIN_DLL)
	@cp $(JSON_NAME) $(PLUGIN_JSON)
	@echo "Build and deploy complete: $(BUILD_DLL)"

$(PLUGIN_DIR):
	@mkdir -p $(PLUGIN_DIR)

clean:
	dotnet clean

# Usage: make package [RELEASE_TAG=v1.0.0]
# Bumps AssemblyVersion patch in all three manifest files, then zips for release.
package:
	@echo "Creating release package..."
	@if command -v jq >/dev/null 2>&1; then \
		CURRENT=$$(jq -r '.[0].AssemblyVersion' repo.json); \
		MAJOR=$$(echo $$CURRENT | cut -d. -f1); \
		MINOR=$$(echo $$CURRENT | cut -d. -f2); \
		PATCH=$$(echo $$CURRENT | cut -d. -f3); \
		BUILD=$$(echo $$CURRENT | cut -d. -f4); \
		NEW_BUILD=$$((BUILD + 1)); \
		NEW_VER="$$MAJOR.$$MINOR.$$PATCH.$$NEW_BUILD"; \
		TS=$$(date +%s); \
		REPO_URL=$$(jq -r '.[0].RepoUrl' repo.json); \
		if [ -n "$$RELEASE_TAG" ]; then \
			DL_URL="$$REPO_URL/releases/download/$$RELEASE_TAG/$(PROJECT_NAME).zip"; \
			jq --arg ts $$TS --arg ver $$NEW_VER --arg url $$DL_URL \
				'.[0].LastUpdate = ($$ts | tonumber) | .[0].AssemblyVersion = $$ver | .[0].DownloadLinkInstall = $$url | .[0].DownloadLinkUpdate = $$url | .[0].DownloadLinkTesting = $$url' \
				repo.json > repo.json.tmp && mv repo.json.tmp repo.json; \
		else \
			jq --arg ts $$TS --arg ver $$NEW_VER \
				'.[0].LastUpdate = ($$ts | tonumber) | .[0].AssemblyVersion = $$ver' \
				repo.json > repo.json.tmp && mv repo.json.tmp repo.json; \
		fi; \
		jq --arg ver $$NEW_VER '.AssemblyVersion = $$ver' $(JSON_NAME) > $(JSON_NAME).tmp && mv $(JSON_NAME).tmp $(JSON_NAME); \
		sed -i.bak "s/\"AssemblyVersion\": \"[^\"]*\"/\"AssemblyVersion\": \"$$NEW_VER\"/" $(YAML_NAME) && rm -f $(YAML_NAME).bak; \
		echo "Version bumped to $$NEW_VER in all three manifest files"; \
	else \
		echo "jq not found - version not bumped. Install jq for automatic version management."; \
	fi
	dotnet build -c $(CONFIGURATION)
	@mkdir -p dist
	@rm -f dist/$(PROJECT_NAME).zip
	@cd $(BUILD_DIR) && \
		zip -q ../../dist/$(PROJECT_NAME).zip $(DLL_NAME) && \
		([ -f $(DLL_NAME:.dll=.deps.json) ] && zip -q ../../dist/$(PROJECT_NAME).zip $(DLL_NAME:.dll=.deps.json) || true) && \
		cd ../.. && \
		zip -q dist/$(PROJECT_NAME).zip $(JSON_NAME) $(YAML_NAME)
	@echo "Package ready: dist/$(PROJECT_NAME).zip"
	@unzip -l dist/$(PROJECT_NAME).zip

help:
	@echo "make build              Build and deploy to dev plugins dir"
	@echo "make package            Bump version, build, and zip for release"
	@echo "make package RELEASE_TAG=v1.0.0   Same, with updated download URLs"
	@echo "make clean              Clean build artifacts"
