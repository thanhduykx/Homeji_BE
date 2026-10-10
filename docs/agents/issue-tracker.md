# Issue tracker: GitHub

Issues and PRDs for this repository live in [thanhduykx/Homeji_BE](https://github.com/thanhduykx/Homeji_BE/issues). Use the `gh` CLI from this clone, or pass `--repo thanhduykx/Homeji_BE` explicitly.

## Conventions

- Create: `gh issue create --repo thanhduykx/Homeji_BE --title "..." --body-file <file>`.
- Read: `gh issue view <number> --repo thanhduykx/Homeji_BE --comments`.
- List: `gh issue list --repo thanhduykx/Homeji_BE --state open --json number,title,body,labels`. Read comments for issues being evaluated.
- Comment: `gh issue comment <number> --repo thanhduykx/Homeji_BE --body-file <file>`.
- Apply or remove labels: `gh issue edit <number> --repo thanhduykx/Homeji_BE --add-label <label>` or `--remove-label <label>`.
- Close: `gh issue close <number> --repo thanhduykx/Homeji_BE` after documenting the resolution.

For multiline bodies, write UTF-8 text to a temporary file and use `--body-file`. Check CLI authentication and existing labels before remote writes; do not claim a remote change succeeded without verifying it.

When a skill says "publish to the issue tracker", create a GitHub issue. When it says "fetch the relevant ticket", read the issue and its comments.

## Pull requests as a triage surface

**PRs as a request surface: no.** Only issues enter the triage queue. Keep external PRs and collaborators' development PRs outside that queue.

GitHub shares issue and PR numbers. Confirm a referenced number is an issue before applying issue triage.

## Repository scope

Backend work belongs in this repository; frontend work belongs in `fogit-hd/EXE101_Homeji`. For a change spanning both, create linked issues in the respective repositories and record dependencies explicitly.

