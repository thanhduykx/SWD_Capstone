import { useEffect, useState, useCallback, useRef } from "react";
import { apiClient } from "../api/client";
import { isAxiosError } from "axios";
import { useAuth } from "../auth/AuthContext";

interface Semester {
  id: number;
  code: string;
  name: string;
  academicYear: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
}

interface GroupMember {
  id: number;
  code: string;
  fullName: string;
  classCode: string;
  major: string;
  email: string;
  isLeader: boolean;
}

interface CapstoneGroup {
  id: number;
  code: string;
  topicName: string;
  supervisorName: string;
  status: string;
  members: GroupMember[];
}

interface ImportResult {
  groupsCreated: number;
  topicsCreated: number;
  studentsCreated: number;
  studentAccountsCreated: number;
  emailsSent: number;
  emailsFailed: number;
}

export function SemestersPage() {
  const [semesters, setSemesters] = useState<Semester[]>([]);
  const [selectedSemesterId, setSelectedSemesterId] = useState<number | null>(null);
  const [groups, setGroups] = useState<CapstoneGroup[]>([]);
  const [loadingSemesters, setLoadingSemesters] = useState(false);
  const [loadingGroups, setLoadingGroups] = useState(false);
  const [importing, setImporting] = useState(false);
  const [importResult, setImportResult] = useState<ImportResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [selectedGroupForMembers, setSelectedGroupForMembers] = useState<CapstoneGroup | null>(null);
  
  const fileInputRef = useRef<HTMLInputElement>(null);
  
  const { signOut } = useAuth();

  const handleResetDatabase = async () => {
    if (!window.confirm("WARNING: This will completely delete all database tables and log you out. Are you sure you want to proceed?")) {
      return;
    }

    try {
      await apiClient.post("/test-support/clear-data");
      alert("Database has been reset successfully! You will be logged out now.");
      signOut();
    } catch (err) {
      alert("Failed to reset database. Ensure you are in development mode.");
    }
  };

  // Fetch Semesters
  const fetchSemesters = useCallback(async () => {
    setLoadingSemesters(true);
    setError(null);
    try {
      const response = await apiClient.get<Semester[]>("/semesters");
      setSemesters(response.data);
      if (response.data.length > 0) {
        // Set default selected semester to the active one, or the first one
        const active = response.data.find((s) => s.isActive);
        setSelectedSemesterId(active ? active.id : response.data[0].id);
      }
    } catch (err) {
      if (isAxiosError(err)) {
        setError(err.response?.data?.error || "Failed to load semesters.");
      } else {
        setError("Failed to load semesters.");
      }
    } finally {
      setLoadingSemesters(false);
    }
  }, []);

  // Fetch Groups
  const fetchGroups = useCallback(async (semesterId: number) => {
    setLoadingGroups(true);
    setError(null);
    try {
      const response = await apiClient.get<CapstoneGroup[]>(`/semesters/${semesterId}/groups`);
      setGroups(response.data);
    } catch (err) {
      if (isAxiosError(err)) {
        setError(err.response?.data?.error || "Failed to load groups.");
      } else {
        setError("Failed to load groups.");
      }
    } finally {
      setLoadingGroups(false);
    }
  }, []);

  useEffect(() => {
    fetchSemesters();
  }, [fetchSemesters]);

  useEffect(() => {
    if (selectedSemesterId !== null) {
      fetchGroups(selectedSemesterId);
    }
  }, [selectedSemesterId, fetchGroups]);

  // Handle Import
  const handleImportClick = () => {
    fileInputRef.current?.click();
  };

  const handleFileChange = async (event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    const formData = new FormData();
    formData.append("file", file);

    setImporting(true);
    setError(null);
    setImportResult(null);

    try {
      const response = await apiClient.post<ImportResult>("/import/groups", formData, {
        headers: { "Content-Type": "multipart/form-data" },
      });
      setImportResult(response.data);
      if (selectedSemesterId !== null) {
        fetchGroups(selectedSemesterId);
      }
    } catch (err) {
      if (isAxiosError(err)) {
        setError(err.response?.data?.error || "Import failed. Verify the Excel format.");
      } else {
        setError("Excel import failed.");
      }
    } finally {
      setImporting(false);
      if (fileInputRef.current) {
        fileInputRef.current.value = "";
      }
    }
  };

  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h2>Semester and capstone groups</h2>
          <p>Manage topics, supervisors and student membership by term.</p>
        </div>
        
        <div style={{ display: "flex", gap: "10px", alignItems: "center" }}>
          {loadingSemesters ? (
            <span className="muted">Loading terms...</span>
          ) : (
            <select
              style={{
                padding: "8px 12px",
                borderRadius: "6px",
                border: "1px solid #d8e2ef",
                fontSize: "14px",
                fontWeight: 500,
                outline: "none"
              }}
              value={selectedSemesterId || ""}
              onChange={(e) => setSelectedSemesterId(Number(e.target.value))}
            >
              {semesters.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name} ({s.code}) {s.isActive ? "• Active" : ""}
                </option>
              ))}
            </select>
          )}
          <button className="primary" onClick={() => {}}>Create semester</button>
          <button 
            className="danger" 
            style={{ 
              padding: "8px 12px", 
              background: "#e11d48", 
              color: "#ffffff", 
              border: "none", 
              borderRadius: "6px", 
              fontSize: "14px", 
              fontWeight: 500, 
              cursor: "pointer" 
            }}
            onClick={handleResetDatabase}
          >
            Reset Database
          </button>
        </div>
      </div>

      {error && (
        <div className="error-banner" style={{
          padding: "12px 16px",
          background: "#fee2e2",
          color: "#b91c1c",
          border: "1px solid #fca5a5",
          borderRadius: "8px",
          marginBottom: "20px",
          fontWeight: 500
        }}>
          ⚠️ {error}
        </div>
      )}

      {importResult && (
        <div className="success-banner" style={{
          padding: "20px",
          background: "#f0fdf4",
          color: "#166534",
          border: "1px solid #bbf7d0",
          borderRadius: "8px",
          marginBottom: "20px",
        }}>
          <h4 style={{ margin: "0 0 10px 0", fontSize: "16px", fontWeight: 700 }}>✅ Excel Import Completed Successfully</h4>
          <p style={{ margin: "0 0 10px 0", fontSize: "14px" }}>
            The official group list has been processed. Here is the summary report:
          </p>
          <ul style={{ margin: 0, paddingLeft: "20px", fontSize: "14px", lineHeight: "20px" }}>
            <li>Capstone Groups created: <strong>{importResult.groupsCreated}</strong></li>
            <li>Project Topics registered: <strong>{importResult.topicsCreated}</strong></li>
            <li>Student profile records imported: <strong>{importResult.studentsCreated}</strong></li>
            <li>New User Accounts created (Role: Student): <strong>{importResult.studentAccountsCreated}</strong></li>
            <li>Credentials notification emails sent: <strong>{importResult.emailsSent}</strong></li>
            {importResult.emailsFailed > 0 && (
              <li style={{ color: "#c2410c" }}>Emails failed to deliver: <strong>{importResult.emailsFailed}</strong></li>
            )}
          </ul>
          <button 
            style={{
              marginTop: "12px",
              padding: "4px 10px",
              fontSize: "12px",
              background: "none",
              border: "1px solid #166534",
              borderRadius: "4px",
              color: "#166534",
              cursor: "pointer"
            }}
            onClick={() => setImportResult(null)}
          >
            Dismiss
          </button>
        </div>
      )}

      <article className="panel table-panel">
        <div className="panel-header">
          <h3>Official groups</h3>
          <input
            type="file"
            ref={fileInputRef}
            style={{ display: "none" }}
            accept=".xlsx"
            onChange={handleFileChange}
          />
          <button 
            className="secondary" 
            onClick={handleImportClick} 
            disabled={importing}
            style={{ display: "flex", alignItems: "center", gap: "6px" }}
          >
            {importing ? "Processing Excel..." : "Import Excel"}
          </button>
        </div>
        
        {loadingGroups ? (
          <div style={{ padding: "40px", textAlign: "center" }} className="muted">
            Loading groups list...
          </div>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Group code</th>
                <th>Topic</th>
                <th>Supervisor</th>
                <th style={{ textAlign: "center" }}>Status</th>
              </tr>
            </thead>
            <tbody>
              {groups.length === 0 ? (
                <tr>
                  <td colSpan={4} className="muted" style={{ textAlign: "center", padding: "30px 10px" }}>
                    No official group data has been imported yet.
                  </td>
                </tr>
              ) : (
                groups.map((g) => (
                  <tr key={g.id} onClick={() => setSelectedGroupForMembers(g)} style={{ cursor: "pointer" }}>
                    <td><strong>{g.code}</strong></td>
                    <td>{g.topicName}</td>
                    <td>{g.supervisorName}</td>
                    <td style={{ textAlign: "center" }}>
                      <span className={`badge ${g.status.toLowerCase()}`} style={{
                        padding: "4px 8px",
                        borderRadius: "12px",
                        fontSize: "12px",
                        fontWeight: 600,
                        background: g.status === "Active" ? "#dcfce7" : "#fee2e2",
                        color: g.status === "Active" ? "#15803d" : "#b91c1c"
                      }}>
                        {g.status}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        )}
      </article>

      {selectedGroupForMembers && (
        <div className="modal-backdrop" role="dialog" aria-modal="true" aria-labelledby="group-details-title">
          <article className="modal" style={{ maxWidth: "800px", width: "100%" }}>
            <h3 id="group-details-title" style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
              <span>Group Details: {selectedGroupForMembers.code}</span>
              <button 
                onClick={() => setSelectedGroupForMembers(null)}
                style={{
                  background: "none",
                  border: "none",
                  fontSize: "24px",
                  cursor: "pointer",
                  color: "#667085",
                  padding: "0"
                }}
              >
                &times;
              </button>
            </h3>
            
            <div style={{ marginBottom: "20px" }}>
              <p style={{ margin: "5px 0" }}><strong>Topic:</strong> {selectedGroupForMembers.topicName}</p>
              <p style={{ margin: "5px 0" }}><strong>Supervisor:</strong> {selectedGroupForMembers.supervisorName}</p>
            </div>

            <h4>Group Members</h4>
            <div style={{ overflowX: "auto" }}>
              <table style={{ width: "100%", borderCollapse: "collapse", marginTop: "10px" }}>
                <thead>
                  <tr style={{ borderBottom: "2px solid #e1e7f0", textAlign: "left" }}>
                    <th style={{ padding: "10px 8px" }}>Role</th>
                    <th style={{ padding: "10px 8px" }}>MSSV</th>
                    <th style={{ padding: "10px 8px" }}>Full Name</th>
                    <th style={{ padding: "10px 8px" }}>Class</th>
                    <th style={{ padding: "10px 8px" }}>Major</th>
                    <th style={{ padding: "10px 8px" }}>Email</th>
                  </tr>
                </thead>
                <tbody>
                  {selectedGroupForMembers.members && selectedGroupForMembers.members.length > 0 ? (
                    selectedGroupForMembers.members.map((m) => (
                      <tr key={m.id} style={{ borderBottom: "1px solid #e1e7f0" }}>
                        <td style={{ padding: "10px 8px" }}>
                          <span style={{
                            padding: "3px 6px",
                            borderRadius: "4px",
                            fontSize: "11px",
                            fontWeight: 700,
                            background: m.isLeader ? "#ffedd5" : "#f1f5f9",
                            color: m.isLeader ? "#c2410c" : "#475569"
                          }}>
                            {m.isLeader ? "Leader" : "Member"}
                          </span>
                        </td>
                        <td style={{ padding: "10px 8px" }}><strong>{m.code}</strong></td>
                        <td style={{ padding: "10px 8px" }}>{m.fullName}</td>
                        <td style={{ padding: "10px 8px" }}>{m.classCode}</td>
                        <td style={{ padding: "10px 8px" }}>{m.major}</td>
                        <td style={{ padding: "10px 8px", fontSize: "13px" }} className="muted">{m.email}</td>
                      </tr>
                    ))
                  ) : (
                    <tr>
                      <td colSpan={6} style={{ textAlign: "center", padding: "20px", color: "#667085" }}>
                        No members found in this group.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>

            <div className="button-row" style={{ marginTop: "24px", display: "flex", justifyContent: "flex-end" }}>
              <button className="primary" type="button" onClick={() => setSelectedGroupForMembers(null)}>Close</button>
            </div>
          </article>
        </div>
      )}
    </section>
  );
}
