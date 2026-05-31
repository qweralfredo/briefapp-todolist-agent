# Skills: GitHub Pull Request & Issues

> **Type:** Skills from the `github.vscode-pull-request-github` extension  
> **Agent:** GitHub Copilot Chat (with GitHub Pull Requests extension)  
> **When to use:** Work with GitHub issues, PRs, and notifications directly in VS Code

---

## What They Are

A set of 4 skills provided by the **GitHub Pull Requests and Issues** extension for Copilot Chat:

| Skill | When to use |
|---|---|
| `summarize-github-issue-pr-notification` | Summarize an issue, PR, or notification |
| `suggest-fix-issue` | Suggest a structured fix for an issue |
| `form-github-search-query` | Build a search query for issues/PRs |
| `show-github-search-result` | Display search results as a table |

---

## How to Install

### 1. Install the extension

```bash
# Via terminal
code --install-extension GitHub.vscode-pull-request-github

# Or via UI: Extensions (Ctrl+Shift+X) -> search "GitHub Pull Requests"
```

### 2. Authenticate with GitHub

1. Open the command palette: `Ctrl+Shift+P`
2. Run: `GitHub Pull Requests: Sign In`
3. Follow the OAuth flow in the browser
4. Confirm authentication: GitHub icon in the sidebar

### 3. Verify the skills are available

In Copilot Chat, skills are loaded automatically after authentication. To confirm, ask:

```
@copilot What skills do you have available?
```

---

## How to Use Each Skill

### summarize-github-issue-pr-notification

Automatic summary of an issue, PR, or notification. **Always use when mentioning an issue/PR.**

```
Summarize issue #42 from the current repository.

Summarize PR #15 — what changed?

What's in the latest GitHub notification?
```

### suggest-fix-issue

Generates a structured fix suggestion for an issue.

```
Suggest a fix for issue #88.

Based on issue #42, how should I fix the problem?
```

### form-github-search-query

Creates optimized search queries for GitHub.

```
Create a query to find open issues about "login"
labeled as bug in this repository.

I want to find PRs merged in the last 7 days by me.
```

### show-github-search-result

Formats and displays search results as a readable Markdown table.

```
Show the results of searching open issues with label "enhancement".
```

---

## Recommended Workflow

```
issue mentioned
    |
summarize-github-issue-pr-notification   (understand the context)
    |
suggest-fix-issue                        (get structured suggestion)
    |
Implement via TDD                        (Red -> Green -> Refactor)
    |
Commit + close issue via PR
```

---

## Repository Permission Configuration

For skills to access private repositories, ensure:

1. You are authenticated in the GitHub Pull Requests extension
2. You have at least `read` permission on the repository
3. The repository is configured as `remote origin` in the workspace

```bash
# Verify remote
git remote -v
# Should show: origin  https://github.com/<owner>/<repo>.git
```

---

## References

- [GitHub Pull Requests Extension](https://marketplace.visualstudio.com/items?itemName=GitHub.vscode-pull-request-github)
- [GitHub Issues in VS Code](https://code.visualstudio.com/docs/sourcecontrol/github)
- [Copilot Chat Skills](https://docs.github.com/en/copilot/using-github-copilot/copilot-chat/using-copilot-chat-in-visual-studio-code)
