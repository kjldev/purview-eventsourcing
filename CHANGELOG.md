# Changelog

## 2.0.0-prerelease.11

### Patch Changes

- Added support for auto-generated mutation events on lists and sets

## 2.0.0-prerelease.10

### Patch Changes

- Added specific list and set types to provide readonly proprties but still support EF etc

## 2.0.0-prerelease.9

### Patch Changes

- Removed OnComputed{Event} partials and added Empty generation

## 2.0.0-prerelease.8

### Patch Changes

- - Fixed OnRaising{Event} partial method generation for methods with computed parameters
  - Added partial method generation for OnCompl(ing|d){Event} methods

## 2.0.0-prerelease.7

### Patch Changes

- Added computed values, enabling deterministic side-effects

## 2.0.0-prerelease.6

### Patch Changes

- Prepare prerelease.6 release

## 2.0.0-prerelease.5

### Patch Changes

- Prepare prerelease.5 release

## 2.0.0-prerelease.4

### Patch Changes

- added support for multi-value value objects

## 2.0.0-prerelease.3

### Patch Changes

- fix for nullable vs. non-nullable properties

## 2.0.0-prerelease.2

### Patch Changes

- source generator has enum field gen and scalar gen fix for equality

## 2.0.0-Init20Release.1

### Patch Changes

- Complete re-write of the source generator

All notable changes to this project will be documented in this file. See [commit-and-tag-version](https://github.com/absolute-version/commit-and-tag-version) for commit guidelines.

## [1.1.2](https://github.com/kjldev/purview-eventsourcing/compare/v1.1.1...v1.1.2) (2026-04-26)

## 1.1.1 (2026-04-20)

### Bug Fixes

- make `EventSourcing.Shared` packable and include it in package output
- align package IDs to the `Purview.EventSourcing*` prefix across packages and documentation
- reinforce deterministic NuGet package build defaults for packable projects

## [0.0.1](https://github.com/kjldev/purview-eventsourcing/compare/v0.0.1-prerelease.0...v0.0.1) (2026-04-16)

### Bug Fixes

- publish draft release after asset upload in CD workflow ([422c3d5](https://github.com/kjldev/purview-eventsourcing/commit/422c3d520fa9257bcfcaedc3df5b73ba8a53a8ca))
- support immutable GitHub releases in CD workflow ([b1489cc](https://github.com/kjldev/purview-eventsourcing/commit/b1489cc251adf527d1f73f01eb8e18129e403c39))

## 1.1.0 (2025-03-03)

### Features

- added snapshot counter telemetry ([a69dc1a](https://github.com/purview-dev/purview-eventsourcing/commit/a69dc1a993ae5caa195a01d6861ad07f27eff948))
- adding storage implementations ([efacc31](https://github.com/purview-dev/purview-eventsourcing/commit/efacc31bac4c34499917ab59d316039cffa12827))
- initial commit ([0b7a104](https://github.com/purview-dev/purview-eventsourcing/commit/0b7a10400d7651bffb551d976e0549ae90323ae8))

### Bug Fixes

- fixed tests ([1bf75c6](https://github.com/purview-dev/purview-eventsourcing/commit/1bf75c62e24ee00e221b760a4e9c97e5e7264260))
