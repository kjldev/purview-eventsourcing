---
name: Lefthook Integration
description: Lefthook Git hook integration.
category: devops
roles:
  - lefthook
  - lefthook-integration
  - devops
tags:
  - git-hooks
  - lefthook
---

# Lefthook Integration Skill

Use this skill for repo-local Git hook setup.

## Rules

- Prefer small, fast checks in pre-commit.
- Put slower checks in pre-push.
- Do not block commits with network-dependent checks.
- Keep commands cross-platform where possible.
- Document how to install and run hooks locally.
