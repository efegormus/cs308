import { BrowserRouter, Route, Routes } from "react-router";
import Layout from "./components/Layout.jsx";
import CartPage from "./pages/CartPage.jsx";
import CheckoutPage from "./pages/CheckoutPage.jsx";
import DeliveriesPage from "./pages/DeliveriesPage.jsx";
import HomePage from "./pages/HomePage.jsx";
import LoginPage from "./pages/LoginPage.jsx";
import OrderConfirmationPage from "./pages/OrderConfirmationPage.jsx";
import OrdersPage from "./pages/OrdersPage.jsx";
import ProductPage from "./pages/ProductPage.jsx";
import ProductsPage from "./pages/ProductsPage.jsx";
import RegisterPage from "./pages/RegisterPage.jsx";
import { DashboardPage, ManagePage, SupportPage } from "./pages/RoleHomePages.jsx";

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route path="/" element={<HomePage />} />
          <Route path="/products" element={<ProductsPage />} />
          <Route path="/product" element={<ProductPage />} />
          <Route path="/cart" element={<CartPage />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/register" element={<RegisterPage />} />
          <Route path="/checkout" element={<CheckoutPage />} />
          <Route path="/order-confirmation" element={<OrderConfirmationPage />} />
          <Route path="/orders" element={<OrdersPage />} />
          <Route path="/deliveries" element={<DeliveriesPage />} />
          <Route path="/dashboard" element={<DashboardPage />} />
          <Route path="/manage" element={<ManagePage />} />
          <Route path="/support" element={<SupportPage />} />
        </Route>
      </Routes>
    </BrowserRouter>
  );
}
