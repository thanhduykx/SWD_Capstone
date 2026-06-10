import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";
import { useLanguage } from "../i18n/LanguageContext";

export function AppLayout() {
  const { language, setLanguage, t } = useLanguage();
  const { loginCode, role, signOut } = useAuth();
  const navigate = useNavigate();
  const navigation = getNavigation(role, t);

  function handleSignOut() {
    signOut();
    navigate("/login", { replace: true });
  }

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark">CP</span>
          <div>
            <strong>CPMS</strong>
            <small>FPT University</small>
          </div>
        </div>
        <nav>
          {navigation.map((item) => (
            <NavLink key={item.to} to={item.to} end={item.to === "/"}>
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
      <main className="content">
        <header className="topbar">
          <div>
            <p className="eyebrow">{t.appEyebrow}</p>
            <h1>{t.appTitle}</h1>
          </div>
          <label className="language-switcher">
            {t.language}
            <select value={language} onChange={(event) => setLanguage(event.target.value === "en" ? "en" : "vi")}>
              <option value="vi">VN</option>
              <option value="en">EN</option>
            </select>
          </label>
          <div className="user-chip">
            <strong>{loginCode ?? t.trainingDepartment}</strong>
            <small>{t.adminStaff}</small>
          </div>
          <button className="secondary" type="button" onClick={handleSignOut}>{t.signOut}</button>
        </header>
        <Outlet />
      </main>
    </div>
  );
}

function getNavigation(role: string | null, t: ReturnType<typeof useLanguage>["t"]) {
  if (role === "SystemAdministrator" || role === "TrainingDepartment") {
    return [
      { to: "/admin", label: t.admin },
      { to: "/training", label: t.training },
    ];
  }

  return [
    { to: "/", label: t.dashboard },
    { to: "/semesters", label: t.semesters },
    { to: "/reviews", label: t.reviews },
    { to: "/defense", label: t.defense },
    { to: "/documents", label: t.documents },
  ];
}
