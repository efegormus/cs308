import { useEffect, useState } from "react";
import { Link } from "react-router";

// The two featured categories. Their links point at the matching category once /api/categories answers.
const FEATURED = [
  { name: "Phones", text: "Latest smartphones" },
  { name: "Tablets", text: "Tablets for work and fun" }
];

// Same request as the old js/home-categories.js. Replace with the shared catalog API client (SCRUM-109) once it is merged.
async function fetchCategories(signal) {
  const response = await fetch("/api/categories", { signal, headers: { Accept: "application/json" } });
  if (!response.ok) throw new Error("Catalog request failed.");
  const rows = await response.json();
  if (!Array.isArray(rows)) throw new Error("Invalid catalog response.");
  return rows;
}

function categoryLink(categories, name) {
  const category = categories.find(item => typeof item.name === "string"
    && item.name.toLowerCase() === name.toLowerCase());
  return category && Number.isInteger(category.id) ? `/products#category-${category.id}` : "/products";
}

export default function HomePage() {
  const [categories, setCategories] = useState([]);

  useEffect(() => {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 15000);
    fetchCategories(controller.signal)
      .then(setCategories)
      // On failure the links open the catalog, whose loading/error/retry UI handles the outage.
      .catch(() => {})
      .finally(() => clearTimeout(timeout));
    return () => { clearTimeout(timeout); controller.abort(); };
  }, []);

  return (
    <div className="route-page home-page">
      <title>Online Store</title>
      <main id="main-content">
        <section className="hero">
          <h1>Welcome to Online Store</h1>
          <p>Find the best phones and tablets at great prices.</p>
          <p><Link to="/products">Browse products by category</Link></p>
        </section>

        <section className="categories" aria-label="Featured categories">
          {FEATURED.map(({ name, text }) => (
            <div className="category" key={name}>
              <h2>{name}</h2>
              <p>{text}</p>
              <Link to={categoryLink(categories, name)}>Browse {name.toLowerCase()}</Link>
            </div>
          ))}
        </section>
      </main>
    </div>
  );
}
