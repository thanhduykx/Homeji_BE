# Domain docs

## Layout

This backend repository uses a **single-context** layout. Read root `CONTEXT.md` for domain vocabulary and relevant decisions in `docs/adr/` before exploring or changing the affected area.

If a root `CONTEXT-MAP.md` is introduced later, follow its links and read the contexts relevant to the task instead.

## Consumer rules

- If context files or ADR directories do not exist, proceed silently. Do not create placeholders merely to complete setup. Domain modeling records terminology and decisions when they are resolved.
- Use the terms defined in the glossary in issues, implementation, hypotheses, and tests. Avoid synonyms the glossary rejects.
- Surface conflicts between proposed changes and existing ADRs explicitly; do not silently override a decision.
- Keep frontend and backend implementation context scoped to the respective repository. For cross-repository changes, inspect both repositories and link relevant issues and decisions.
