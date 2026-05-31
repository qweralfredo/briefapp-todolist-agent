# Repository Governance: Squash & Merge Policy

To maintain a clean and semantic history on the `main` branch, this project follows the **Squash & Merge** pattern combined with **Conventional Commits**.

## GitHub Settings Configuration (Admin Action Required)
1. Go to **Settings > Pull Requests**.
2. **Allow squash merging:** ENABLED.
3. **Allow merge commits:** DISABLED.
4. **Allow rebase merging:** DISABLED.
5. **Squash merge commit title:** Default to Pull Request title.
6. **Squash merge commit message:** Default to Pull Request body.

## Why Squash & Merge?
- **Impeccable History:** The `main` branch only contains atomic, functional units of work.
- **Traceability:** Each commit on `main` links back to a Pull Request.
- **Automation:** Conventional Commits in the PR title allow for automated changelog generation.
- **Atomic-Agent Flow:** Supports the "Ephemeral Branches" protocol where small, experimental commits are squashed into a single, high-quality commit upon integration.
