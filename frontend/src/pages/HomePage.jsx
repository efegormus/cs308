import { Link } from "react-router";

// Skeleton. SCRUM-112 builds the home page.
export default function HomePage() {
  return (
    <div className="route-page home-page">
      <title>Online Store</title>
      <main id="main-content">
        <section className="hero">
          <h1>Welcome to Online Store</h1>
          <p>Find the best phones and tablets at great prices.</p>
          <p><Link to="/products">Browse products by category</Link></p>
        </section>
      </main>
    </div>
  );
}
