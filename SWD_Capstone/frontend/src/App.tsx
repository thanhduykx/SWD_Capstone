import { Navigate, Route, Routes } from "react-router-dom";
import { AppLayout } from "./components/AppLayout";
import { DashboardPage } from "./pages/DashboardPage";
import { DefensePage } from "./pages/DefensePage";
import { DocumentsPage } from "./pages/DocumentsPage";
import { LoginPage } from "./pages/LoginPage";
import { ReviewsPage } from "./pages/ReviewsPage";
import { SemestersPage } from "./pages/SemestersPage";

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<AppLayout />}>
        <Route index element={<DashboardPage />} />
        <Route path="/semesters" element={<SemestersPage />} />
        <Route path="/reviews" element={<ReviewsPage />} />
        <Route path="/defense" element={<DefensePage />} />
        <Route path="/documents" element={<DocumentsPage />} />
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
