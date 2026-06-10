import { Navigate, Route, Routes } from "react-router-dom";
import { RequireAuth } from "./auth/RequireAuth";
import { AppLayout } from "./components/AppLayout";
import { AdminPage } from "./pages/AdminPage";
import { DashboardPage } from "./pages/DashboardPage";
import { DefensePage } from "./pages/DefensePage";
import { DocumentsPage } from "./pages/DocumentsPage";
import { LoginPage } from "./pages/LoginPage";
import { ReviewsPage } from "./pages/ReviewsPage";
import { SemestersPage } from "./pages/SemestersPage";
import { TrainingDepartmentPage } from "./pages/TrainingDepartmentPage";

export function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth allowedRoles={["TrainingDepartment", "SystemAdministrator"]} />}>
        <Route element={<AppLayout />}>
          <Route path="/admin" element={<AdminPage />} />
        </Route>
      </Route>
      <Route element={<RequireAuth allowedRoles={["TrainingDepartment", "SystemAdministrator"]} />}>
        <Route element={<AppLayout />}>
          <Route path="/training" element={<TrainingDepartmentPage />} />
        </Route>
      </Route>
      <Route element={<RequireAuth allowedRoles={["Lecturer"]} />}>
        <Route element={<AppLayout />}>
          <Route index element={<DashboardPage />} />
          <Route path="/semesters" element={<SemestersPage />} />
          <Route path="/reviews" element={<ReviewsPage />} />
          <Route path="/defense" element={<DefensePage />} />
          <Route path="/documents" element={<DocumentsPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  );
}
