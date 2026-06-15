import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { useLanguage } from "../i18n/LanguageContext";

export function AppLayout() {
  const { language, setLanguage, t } = useLanguage();
  const { loginCode, role, signOut } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const navigation = getNavigation(role, t);

  function handleSignOut() {
    signOut();
    navigate("/login", { replace: true });
  }

  return (
    <div className="app-container shell">
      <aside className="sidebar">
        <div className="sidebar-header brand">
          <span className="sidebar-logo-icon brand-mark">
            <AppIcon name="review" />
          </span>
          <div>
            <strong className="sidebar-title">CPMS</strong>
            <small className="sidebar-subtitle">FPT University</small>
          </div>
        </div>
        <nav className="sidebar-menu">
          {navigation.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.to === "/"} className="sidebar-menu-item">
              <AppIcon name={item.icon} />
              {item.label}
            </NavLink>
          ))}
        </nav>
        <div className="sidebar-note">
          <small>{t.activeSemester}</small>
          <strong>{t.noActiveSemester}</strong>
          <span>{t.evaluationCycle}</span>
        </div>
      </aside>
      <div className="main-wrapper">
        <header className="app-header topbar">
          <div className="header-breadcrumbs">
            <span>{t.appEyebrow}</span>
            <span className="breadcrumb-separator">/</span>
            <span className="breadcrumb-active">{activePageLabel(navigation, location.pathname)}</span>
          </div>
          <div className="header-actions">
            <label className="language-switcher">
              {t.language}
              <select value={language} onChange={(event) => setLanguage(event.target.value === "en" ? "en" : "vi")}>
                <option value="vi">VN</option>
                <option value="en">EN</option>
              </select>
            </label>
            <div className="notification-bell" title="Notifications">
              <AppIcon name="bell" />
              <span className="notification-badge">3</span>
            </div>
            <div className="user-profile user-chip">
              <span className="user-avatar" aria-hidden="true">{userInitial(loginCode)}</span>
              <div className="user-info">
                <strong className="user-name">{loginCode ?? t.trainingDepartment}</strong>
                <small className="user-role">{roleLabel(role, t)}</small>
              </div>
            </div>
            <button className="secondary sidebar-logout" type="button" onClick={handleSignOut}>
              <AppIcon name="logout" />
              {t.signOut}
            </button>
          </div>
        </header>
        <main className="content page-container">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function getNavigation(role: string | null, t: ReturnType<typeof useLanguage>["t"]) {
  if (role === "SystemAdministrator" || role === "TrainingDepartment") {
    return [
      { to: "/admin", label: t.admin, icon: "users" },
      { to: "/training", label: t.training, icon: "calendar" },
      { to: "/semesters", label: t.semesters, icon: "semester" },
    ];
  }

  if (role === "Student") {
    return [
      { to: "/student", label: t.studentDashboard ?? "Dashboard", icon: "dashboard" },
      { to: "/student/register", label: t.studentRegister ?? "Đăng ký slot", icon: "calendar" },
      { to: "/student/schedule", label: t.studentSchedule ?? "Lịch review", icon: "review" },
    ];
  }

  return [
    { to: "/", label: t.dashboard, icon: "dashboard" },
    { to: "/semesters", label: t.semesters, icon: "semester" },
    { to: "/reviews", label: t.reviews, icon: "review" },
    { to: "/defense", label: t.defense, icon: "defense" },
    { to: "/documents", label: t.documents, icon: "document" },
  ];
}

function activePageLabel(navigation: Array<{ to: string; label: string }>, currentPath: string) {
  return navigation.find((item) => item.to === currentPath || (item.to === "/" && currentPath === "/"))?.label ?? "CPMS";
}

function userInitial(loginCode: string | null) {
  return (loginCode?.trim().charAt(0) || "C").toUpperCase();
}

function roleLabel(role: string | null, t: ReturnType<typeof useLanguage>["t"]) {
  if (role === "Lecturer") {
    return t.lecturer;
  }

  if (role === "Student") {
    return t.student;
  }

  return t.moderator;
}

function AppIcon({ name }: { name: string }) {
  const paths: Record<string, string> = {
    dashboard: "M4 13h7V4H4v9Zm0 7h7v-5H4v5Zm9 0h7v-9h-7v9Zm0-16v5h7V4h-7Z",
    users: "M16 11c1.66 0 3-1.34 3-3s-1.34-3-3-3-3 1.34-3 3 1.34 3 3 3ZM8 11c1.66 0 3-1.34 3-3S9.66 5 8 5 5 6.34 5 8s1.34 3 3 3Zm8 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5Zm-8 0c-.29 0-.62.02-.97.05C5.1 13.23 1 14.17 1 16.5V19h6v-2.5c0-.83.32-1.59.91-2.25.28-.31.62-.6 1.01-.85A9.45 9.45 0 0 0 8 13Z",
    calendar: "M7 2v2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2h-2V2h-2v2H9V2H7Zm12 8H5v10h14V10Z",
    semester: "M12 3 2 8l10 5 10-5-10-5Zm-7 8v5l7 3.5 7-3.5v-5l-7 3.5L5 11Z",
    review: "M19 3H5a2 2 0 0 0-2 2v14l4-4h12a2 2 0 0 0 2-2V5a2 2 0 0 0-2-2Zm-9 9-3-3 1.4-1.4L10 9.17l4.6-4.6L16 6l-6 6Z",
    defense: "M12 2 4 5v6c0 5 3.4 9.7 8 11 4.6-1.3 8-6 8-11V5l-8-3Zm-1 14-4-4 1.4-1.4 2.6 2.58 5.6-5.58L18 9l-7 7Z",
    document: "M6 2h9l5 5v15H6V2Zm8 1.5V8h4.5L14 3.5ZM8 12h8v2H8v-2Zm0 4h8v2H8v-2Z",
    bell: "M12 22a2.5 2.5 0 0 0 2.45-2h-4.9A2.5 2.5 0 0 0 12 22Zm7-6v-5a7 7 0 0 0-5-6.71V3a2 2 0 1 0-4 0v1.29A7 7 0 0 0 5 11v5l-2 2v1h18v-1l-2-2Z",
    logout: "M10 17v-3H3v-4h7V7l5 5-5 5Zm2 4h7V3h-7v2h5v14h-5v2Z",
  };

  return (
    <svg aria-hidden="true" className="app-icon" viewBox="0 0 24 24" focusable="false">
      <path d={paths[name] ?? paths.dashboard} />
    </svg>
  );
}
