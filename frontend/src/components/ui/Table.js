// Data table (SCRUM-124). Plain JavaScript React elements, like RequestStates.js, so Node can render and test it; for the
// same reason it does not import its stylesheet: a page that uses it imports "./ui/Table.css" (see docs/ui-design.md).
//
// columns: [{ key, header, render?(row), rowHeader?, align?: "end" }]. A column with rowHeader renders <th scope="row">.
// While loading it shows skeleton rows inside a role="status" box named by loadingLabel; with no rows it shows emptyText
// in a role="status" box. On a narrow screen the table scrolls sideways inside its frame, never the page.
import { createElement as h } from "react";

const cellClass = column => column.align === "end" ? "ui-table-cell ui-table-cell--end" : "ui-table-cell";

function head(columns) {
  return h("thead", null, h("tr", null, columns.map(column =>
    h("th", { key: column.key, scope: "col", className: cellClass(column) }, column.header))));
}

function cell(column, row) {
  const content = typeof column.render === "function" ? column.render(row) : row[column.key];
  return column.rowHeader
    ? h("th", { key: column.key, scope: "row", className: cellClass(column) }, content)
    : h("td", { key: column.key, className: cellClass(column) }, content);
}

export default function Table({
  caption, columns = [], rows = [], rowKey = row => row.id, loading = false,
  loadingLabel = "Loading…", emptyText = "Nothing to show yet.", skeletonRows = 4, className = ""
} = {}) {
  const frame = className ? `ui-table-frame ${className}` : "ui-table-frame";
  if (loading) {
    const skeleton = Array.from({ length: skeletonRows }, (_, index) => h("tr", { key: index, className: "ui-table-skeleton-row" },
      columns.map(column => h("td", { key: column.key, className: "ui-table-cell" }, h("span", { className: "ui-table-skeleton" })))));
    return h("div", { className: `${frame} ui-table-frame--loading`, role: "status", "aria-label": loadingLabel, "aria-busy": true },
      h("span", { className: "ui-table-sr" }, loadingLabel),
      h("div", { className: "ui-table-scroll" },
        h("table", { className: "ui-table", "aria-hidden": true }, head(columns), h("tbody", null, skeleton))));
  }
  if (rows.length === 0) {
    return h("div", { className: `${frame} ui-table-frame--empty` },
      caption && h("p", { className: "ui-table-caption" }, caption),
      h("p", { className: "ui-table-empty", role: "status" }, emptyText));
  }
  // The scroll box is focusable and named, so a keyboard user can scroll a wide table too.
  return h("div", { className: frame },
    h("div", { className: "ui-table-scroll", tabIndex: 0, role: "region", "aria-label": caption || undefined },
      h("table", { className: "ui-table" },
        caption && h("caption", { className: "ui-table-caption" }, caption),
        head(columns),
        h("tbody", null, rows.map(row => h("tr", { key: rowKey(row) }, columns.map(column => cell(column, row))))))));
}
