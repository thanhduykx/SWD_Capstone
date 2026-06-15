import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { apiClient } from "../api/client";

type ReviewType = "Review1" | "Review2" | "Review3";
type ReviewRoundStatus = "Draft" | "Open" | "Closed" | "Published";

type ReviewRound = {
  id: number;
  semesterId: number;
  type: ReviewType;
  status: ReviewRoundStatus;
  weekStartDate: string;
  weekEndDate: string;
  isGroupRegistered: boolean;
};

type ScheduleItem = {
  sessionId: number;
  sessionCode: string;
  type: ReviewType;
  sessionDate: string;
  slot: number;
  room: string;
};

const statusConfig: Record<ReviewRoundStatus, { label: string; color: string; description: string }> = {
  Draft: { label: "Chưa mở", color: "var(--text-muted)", description: "Đợt review chưa được mở đăng ký." },
  Open: { label: "Đang mở đăng ký", color: "var(--success)", description: "Bạn có thể đăng ký slot review ngay!" },
  Closed: { label: "Đã đóng đăng ký", color: "var(--warning)", description: "Moderator đang sắp xếp lịch." },
  Published: { label: "Đã publish lịch", color: "var(--info)", description: "Xem lịch review tại tab Lịch review." },
};

export function StudentDashboardPage() {
  const [rounds, setRounds] = useState<ReviewRound[]>([]);
  const [schedule, setSchedule] = useState<ScheduleItem[]>([]);
  const navigate = useNavigate();

  useEffect(() => {
    void loadData();
  }, []);

  async function loadData() {
    try {
      const [roundsRes, scheduleRes] = await Promise.all([
        apiClient.get<ReviewRound[]>("/student-review/rounds"),
        apiClient.get<ScheduleItem[]>("/student-review/schedule"),
      ]);
      setRounds(roundsRes.data);
      setSchedule(scheduleRes.data);
    } catch {
      // ignore
    }
  }

  const openRounds = rounds.filter((r) => r.status === "Open");
  const upcomingSessions = schedule.filter((s) => new Date(s.sessionDate) >= new Date());

  return (
    <section className="page student-dashboard-page">
      <div className="page-title">
        <div>
          <h2>Dashboard sinh viên</h2>
          <p>Tổng quan đợt review, đăng ký slot và lịch review sắp tới.</p>
        </div>
      </div>

      {/* Metrics */}
      <div className="metric-grid">
        <div className="metric">
          <span>Đợt review</span>
          <strong>{rounds.length}</strong>
          <small>{openRounds.length} đang mở</small>
        </div>
        <div className="metric">
          <span>Đã đăng ký</span>
          <strong>{rounds.filter((r) => r.isGroupRegistered).length}</strong>
          <small>slot review</small>
        </div>
        <div className="metric">
          <span>Lịch review</span>
          <strong>{schedule.length}</strong>
          <small>sessions</small>
        </div>
        <div className="metric">
          <span>Sắp tới</span>
          <strong>{upcomingSessions.length}</strong>
          <small>sessions</small>
        </div>
      </div>

      <div className="dashboard-grid">
        {/* Open review rounds */}
        <article className="panel">
          <div className="panel-header">
            <h3>Đợt review</h3>
            <button className="primary" onClick={() => navigate("/student/register")}>Đăng ký slot →</button>
          </div>
          {rounds.length === 0 && <p className="muted">Chưa có đợt review nào.</p>}
          {rounds.map((round) => {
            const config = statusConfig[round.status];
            return (
              <div
                key={round.id}
                className="checklist-item"
                style={{ marginBottom: 10, cursor: round.status === "Open" ? "pointer" : "default" }}
                onClick={() => round.status === "Open" && navigate("/student/register")}
              >
                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <div>
                    <strong>{round.type}</strong>
                    <span className="muted" style={{ marginLeft: 12 }}>
                      {formatDateOnly(round.weekStartDate)} – {formatDateOnly(round.weekEndDate)}
                    </span>
                  </div>
                  <span className="tag" style={{ background: config.color + "22", color: config.color }}>
                    {config.label}
                  </span>
                </div>
                <small className="muted">{config.description}</small>
                {round.isGroupRegistered && (
                  <small style={{ color: "var(--success)", fontWeight: 700 }}>✅ Nhóm đã đăng ký</small>
                )}
              </div>
            );
          })}
        </article>

        {/* Upcoming sessions */}
        <article className="panel">
          <div className="panel-header">
            <h3>Lịch review sắp tới</h3>
            <button className="secondary" onClick={() => navigate("/student/schedule")}>Xem tất cả →</button>
          </div>
          {upcomingSessions.length === 0 && <p className="muted">Chưa có lịch review nào sắp tới.</p>}
          {upcomingSessions.map((session) => (
            <div key={session.sessionId} className="checklist-item" style={{ marginBottom: 10 }}>
              <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                <strong>{session.type} - {session.sessionCode}</strong>
                <span className="badge">{new Date(session.sessionDate).toLocaleDateString("vi-VN")}</span>
              </div>
              <small className="muted">
                Slot {session.slot} | Phòng {session.room}
              </small>
            </div>
          ))}
        </article>
      </div>
    </section>
  );
}

function formatDateOnly(value: string) {
  const parts = value.split("-");
  return `${parts[2]}/${parts[1]}`;
}
