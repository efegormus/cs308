// The header's category menu (SCRUM-123), built from GET /api/categories. Pure, so Node can test it.
//
// Each category may carry parentId: null for a top-level category, otherwise the id of its parent. A top-level
// category becomes an item of the bar, and every category below it (at any depth) is listed in its panel, in the
// order the API returns them. When a category has no usable parentId (an older server, a test fixture, a parent that
// is missing from the list, or a loop) it counts as top-level, so the menu falls back to a flat list.
export function buildCategoryTree(categories) {
  const valid = (Array.isArray(categories) ? categories : [])
    .filter(category => Number.isInteger(category?.id) && typeof category.name === "string");
  const byId = new Map(valid.map(category => [category.id, category]));

  function rootOf(category) {
    const seen = new Set([category.id]);
    let current = category;
    while (Number.isInteger(current.parentId) && byId.has(current.parentId)) {
      if (seen.has(current.parentId)) return category; // A loop: treat the category as top-level.
      seen.add(current.parentId);
      current = byId.get(current.parentId);
    }
    return current;
  }

  const roots = [];
  const children = new Map();
  for (const category of valid) {
    const root = rootOf(category);
    if (root === category) {
      roots.push(category);
      if (!children.has(category.id)) children.set(category.id, []);
    } else {
      if (!children.has(root.id)) children.set(root.id, []);
      children.get(root.id).push({ id: category.id, name: category.name });
    }
  }
  return roots.map(root => ({ id: root.id, name: root.name, children: children.get(root.id) }));
}

// The top-level category that contains this category id (itself or one of its descendants), or null.
export function activeRootId(tree, categoryId) {
  if (!Number.isInteger(categoryId)) return null;
  const root = tree.find(item => item.id === categoryId || item.children.some(child => child.id === categoryId));
  return root ? root.id : null;
}
