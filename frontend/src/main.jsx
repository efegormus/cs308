import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App.jsx";
import { AuthProvider } from "./auth/AuthProvider.jsx";
import "./styles/design-system.css";
import "./styles/layout.css";
import "./styles/auth.css";
import "./styles/products.css";
import "./styles/purchases.css";
import "./styles/home.css";

createRoot(document.getElementById("root")).render(
  <StrictMode>
    <AuthProvider>
      <App />
    </AuthProvider>
  </StrictMode>
);
