import { NavLink, Outlet } from "react-router-dom";

const navigation = [
  { to: "/", label: "Dashboard" },
  { to: "/semesters", label: "Semesters & Groups" },
  { to: "/reviews", label: "Review Scheduling" },
  { to: "/defense", label: "Defense Scoring" },
  { to: "/documents", label: "Documents & CLO" },
];

export function AppLayout() {
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
          <small>Active semester</small>
          <strong>SP26 - SEP490</strong>
          <span>Document evaluation cycle</span>
        </div>
      </aside>
      <main className="content">
        <header className="topbar">
          <div>
            <p className="eyebrow">CAPSTONE PROJECT MANAGEMENT SYSTEM</p>
            <h1>Syllabus-based evaluation</h1>
          </div>
          <div className="user-chip">
            <strong>Training Department</strong>
            <small>Admin / Staff</small>
          </div>
        </header>
        <Outlet />
      </main>
    </div>
  );
}
