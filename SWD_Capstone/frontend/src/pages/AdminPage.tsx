import { useEffect, useState } from "react";
import { apiClient } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";
import type { FormEvent } from "react";
import type { UserRole } from "../auth/AuthContext";

type Account = {
  id: number;
  username: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  lastLoginAt?: string | null;
};

export function AdminPage() {
  const { t } = useLanguage();
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [role, setRole] = useState<UserRole>("Lecturer");
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("123456");
  const [code, setCode] = useState("");
  const [fullName, setFullName] = useState("");
  const [department, setDepartment] = useState("SE");
  const [position, setPosition] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const accountRoles: Array<{ label: string; value: UserRole; note: string }> = [
    { label: t.lecturer, value: "Lecturer", note: t.lecturerNote },
    { label: t.council, value: "EvaluationPanel", note: t.councilNote },
  ];

  useEffect(() => {
    void loadAccounts();
  }, []);

  async function loadAccounts() {
    const response = await apiClient.get<Account[]>("/accounts");
    setAccounts(response.data);
  }

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setMessage(null);
    setIsLoading(true);

    try {
      await apiClient.post("/accounts", {
        username,
        email,
        password,
        role,
        code,
        fullName,
        department,
        position,
        permissionScope: "System",
        isPartTime: false,
        maxGroups: 0,
        classCode: "",
        batch: "",
        major: "SE",
      });
      setMessage(t.accountCreated);
      setUsername("");
      setEmail("");
      setCode("");
      setFullName("");
      setPosition("");
      await loadAccounts();
    } catch {
      setMessage(t.accountCreateFailed);
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h2>{t.adminTitle}</h2>
          <p>{t.adminSubtitle}</p>
        </div>
      </div>
      <div className="dashboard-grid">
        <article className="panel">
          <h3>{t.createOfficialAccount}</h3>
          <form className="form-grid" onSubmit={handleCreate}>
            <label>
              {t.loginRole}
              <select value={role} onChange={(event) => setRole(event.target.value as UserRole)}>
                {accountRoles.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
              </select>
            </label>
            <p className="muted">{accountRoles.find((item) => item.value === role)?.note}</p>
            <p className="muted">{t.chairmanNote}</p>
            <label>{t.loginCode}<input value={username} onChange={(event) => setUsername(event.target.value)} placeholder={t.loginCodePlaceholder} /></label>
            <label>{t.email}<input value={email} onChange={(event) => setEmail(event.target.value)} placeholder="name@fpt.edu.vn" /></label>
            <label>{t.initialPassword}<input value={password} onChange={(event) => setPassword(event.target.value)} type="password" /></label>
            <label>{t.profileCode}<input value={code} onChange={(event) => setCode(event.target.value)} placeholder={t.loginCodePlaceholder} /></label>
            <label>{t.fullName}<input value={fullName} onChange={(event) => setFullName(event.target.value)} /></label>
            <label>{t.department}<input value={department} onChange={(event) => setDepartment(event.target.value)} /></label>
            <label>{t.roleNote}<input value={position} onChange={(event) => setPosition(event.target.value)} placeholder={t.roleNotePlaceholder} /></label>
            {message && <p className="alert">{message}</p>}
            <button className="primary" type="submit" disabled={isLoading}>{isLoading ? t.creating : t.createAccount}</button>
          </form>
        </article>
        <article className="panel table-panel">
          <div className="panel-header"><h3>{t.accounts}</h3><button className="secondary" onClick={loadAccounts}>{t.refresh}</button></div>
          <table>
            <thead><tr><th>{t.code}</th><th>{t.role}</th><th>{t.status}</th><th>{t.lastLogin}</th></tr></thead>
            <tbody>
              {accounts.length === 0 && <tr><td colSpan={4} className="muted">{t.noAccounts}</td></tr>}
              {accounts.map((account) => (
                <tr key={account.id}>
                  <td>{account.username}<br /><small className="muted">{account.email}</small></td>
                  <td>{account.role === "Lecturer" ? t.lecturer : account.role === "EvaluationPanel" ? t.council : account.role}</td>
                  <td><span className="tag">{account.isActive ? t.active : t.inactive}</span></td>
                  <td>{account.lastLoginAt ? new Date(account.lastLoginAt).toLocaleString() : "-"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </article>
      </div>
    </section>
  );
}
