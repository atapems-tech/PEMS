## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).

# Zaemit Visual Editor - AI Tool Instructions

## MCP Tool Priority

This project uses the **Zaemit Visual HTML/CSS Editor** VS Code extension.

**IMPORTANT**: When the user asks about editing elements, styles, selections, outlines, colors, or anything related to a visual editor / HTML / CSS / web page, ALWAYS use `zaemit_*` tools FIRST — NOT Pencil tools.

### When to use Zaemit tools
- User mentions: "재밋", "zaemit", "에디터", "비주얼 에디터", "HTML", "엘리먼트", "선택한", "외곽선", "스타일", "페이지"
- Any request about editing/viewing/selecting HTML elements
- Any request about CSS styles, outlines, borders, colors, layouts

### When to use Pencil tools
- ONLY when the user explicitly mentions ".pen files" or "pencil"

### Available Zaemit tools
- `zaemit_get_editor_state` - Editor connection status
- `zaemit_get_selection` - Selected HTML element details
- `zaemit_get_page_html` - Full page HTML
- `zaemit_get_element_tree` - DOM tree
- `zaemit_update_element` - Update styles/attributes/text
- `zaemit_replace_element_html` - Replace outerHTML
- `zaemit_insert_element` - Insert new HTML
- `zaemit_delete_element` - Delete element
