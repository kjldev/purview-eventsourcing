---
exclusions:
  - .env
  - .env.*
  - '*.pem'
  - '*.key'
  - '*.pfx'
  - '*.cer'
  - secrets.*
  - .azure/
  - .aws/
  - .ssh/
  - bin/
  - obj/
  - node_modules/
  - dist/
  - build/
  - .next/
  - .turbo/
  - .cache/
  - coverage/
  - .agents/cache/
  - .agents/backups/
  - .agents/tmp/
  - .claude/settings.local.json
  - .opencode/*.local.json
---

# Agent Exclusions

Agents should not read, summarize, or store data from the patterns listed in frontmatter.
