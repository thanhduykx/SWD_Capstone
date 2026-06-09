import { useEffect, useState } from "react";
import { isAxiosError } from "axios";
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
  const [fullName, setFullName] = useState("");
  const [department, setDepartment] = useState("SE");
  const [position, setPosition] = useState("");
  const [classCode, setClassCode] = useState("");
  const [batch, setBatch] = useState("");
  const [major, setMajor] = useState("SE");
  const [message, setMessage] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const accountRoles: Array<{ label: string; value: UserRole; note: string }> = [
    { label: t.student, value: "Student", note: t.studentNote },
    { label: t.lecturer, value: "Lecturer", note: t.lecturerNote },
    { label: t.moderator, value: "TrainingDepartment", note: t.moderatorNote },
  ];
  const selectedRoleNote = accountRoles.find((item) => item.value === role)?.note;

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

    const validationMessage = validateAccountForm(role, username, email, password, fullName, department, classCode);
    if (validationMessage) {
      setMessage(validationMessage);
      return;
    }

    setIsLoading(true);

    try {
      await apiClient.post("/accounts", {
        username: username.trim(),
        email: email.trim(),
        password,
        role,
        fullName: fullName.trim(),
        department: department.trim(),
        position: position.trim(),
        permissionScope: "System",
        isPartTime: false,
        classCode: classCode.trim(),
        batch: batch.trim(),
        major: major.trim(),
      });
      setMessage(t.accountCreated);
      setUsername("");
      setEmail("");
      setFullName("");
      setPosition("");
      setClassCode("");
      setBatch("");
      setMajor("SE");
      await loadAccounts();
    } catch (error) {
      setMessage(getAccountCreateError(error, t.accountCreateFailed));
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
            <p className="muted">{selectedRoleNote}</p>
            <label>{t.loginCode}<input value={username} onChange={(event) => setUsername(event.target.value)} placeholder={t.loginCodePlaceholder} required /></label>
            <label>{t.email}<input value={email} onChange={(event) => setEmail(event.target.value)} placeholder="name@fpt.edu.vn" type="email" required /></label>
            <label>{t.initialPassword}<input value={password} onChange={(event) => setPassword(event.target.value)} type="password" minLength={6} required /></label>
            {(role === "Student" || role === "Lecturer") && (
              <label>{t.fullName}<input value={fullName} onChange={(event) => setFullName(event.target.value)} required /></label>
            )}
            {(role === "Lecturer" || role === "TrainingDepartment") && (
              <label>{t.department}<input value={department} onChange={(event) => setDepartment(event.target.value)} required /></label>
            )}
            {role === "Student" && (
              <>
                <label>{t.classCode}<input value={classCode} onChange={(event) => setClassCode(event.target.value)} placeholder="SE1901" required /></label>
                <label>{t.batchName}<input value={batch} onChange={(event) => setBatch(event.target.value)} placeholder="K19" /></label>
                <label>{t.major}<input value={major} onChange={(event) => setMajor(event.target.value)} placeholder="SE" /></label>
              </>
            )}
            {role === "TrainingDepartment" && (
              <label>{t.roleNote}<input value={position} onChange={(event) => setPosition(event.target.value)} placeholder="Moderator" /></label>
            )}
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
                  <td>{roleLabel(account.role, t)}</td>
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

function roleLabel(role: UserRole, t: ReturnType<typeof useLanguage>["t"]) {
  if (role === "Student") {
    return t.student;
  }

  if (role === "Lecturer") {
    return t.lecturer;
  }

  if (role === "TrainingDepartment") {
    return t.moderator;
  }

  if (role === "EvaluationPanel") {
    return t.council;
  }

  return role;
}

type ApiErrorBody = {
  error?: string;
  title?: string;
};

function validateAccountForm(
  role: UserRole,
  username: string,
  email: string,
  password: string,
  fullName: string,
  department: string,
  classCode: string,
) {
  if (!username.trim() || !email.trim() || !password) {
    return "Thiếu mã đăng nhập, email hoặc mật khẩu.";
  }

  if (password.length < 6) {
    return "Mật khẩu phải có ít nhất 6 ký tự.";
  }

  if ((role === "Student" || role === "Lecturer") && !fullName.trim()) {
    return "Thiếu họ tên.";
  }

  if ((role === "Lecturer" || role === "TrainingDepartment") && !department.trim()) {
    return "Thiếu bộ môn.";
  }

  if (role === "Student" && !classCode.trim()) {
    return "Sinh viên bắt buộc nhập mã lớp.";
  }

  return null;
}

function getAccountCreateError(error: unknown, fallback: string) {
  if (!isAxiosError<ApiErrorBody>(error)) {
    return fallback;
  }

  const apiError = error.response?.data?.error ?? error.response?.data?.title;
  if (!apiError) {
    return fallback;
  }

  const knownMessages: Record<string, string> = {
    "Username, email and password are required.": "Thiếu mã đăng nhập, email hoặc mật khẩu.",
    "Password must contain at least 6 characters.": "Mật khẩu phải có ít nhất 6 ký tự.",
    "Username or email already exists.": "Mã đăng nhập hoặc email đã tồn tại.",
    "Full name is required for lecturers.": "Giảng viên bắt buộc nhập họ tên.",
    "Department is required for lecturers.": "Giảng viên bắt buộc nhập bộ môn.",
    "Full name is required for students.": "Sinh viên bắt buộc nhập họ tên.",
    "Class code is required for students.": "Sinh viên bắt buộc nhập mã lớp.",
  };

  return knownMessages[apiError] ?? apiError;
}
