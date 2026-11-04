# Read first (mandatory for every task)

Read [AGENTS.md](AGENTS.md) and [docs/guidelines/README.md](docs/guidelines/README.md) before doing any task, then the Jira issue and
related issues. Follow the guideline while working; update it at the end if you learned something durable. Say in your first message that you did.

@AGENTS.md

## graphify

If `graphify-out/graph.json` exists, answer codebase questions with `graphify query "<question>"` (relations: `graphify path "<A>" "<B>"`, concepts: `graphify explain "<X>"`)
before reading files; `graphify-out/wiki/index.md` for navigation; `GRAPH_REPORT.md` only for broad architecture review. After modifying code run `graphify update .`.
graphify is optional on a machine (guideline 09 section 4).

## Zaemit Visual Editor

For editing, viewing or selecting HTML elements, styles or the visual editor, use the `zaemit_*` tools first; Pencil tools only for `.pen` files.
