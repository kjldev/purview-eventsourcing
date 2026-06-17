---
name: Changesets Prerelease
description: Create and apply a prerelease bump using Changesets CLI.
category: release
roles:
  - release
  - changesets-prerelease
tags:
  - changesets
  - versioning
  - prerelease
---

# Changesets Prerelease Skill

Use this skill when preparing the next prerelease version in this repository.

## Steps

1. Add a new changeset:
   - `npx @changesets/cli add --empty --message "<summary>"`
   - update the generated `.changeset/*.md` frontmatter with:
     - `"purview-eventsourcing": patch`
2. Bump versions/changelog:
   - `npx @changesets/cli version`
3. Commit the resulting changes (`package.json`, `CHANGELOG.md`, and consumed `.changeset` files).

## Notes

- Repository currently runs prerelease mode with `tag: prerelease` (`.changeset/pre.json`).
- Releasing is done from `package.json` version in the `release.yml` workflow.
