import { useEffect, useState } from "react";
import { isAxiosError } from "axios";
import { apiClient } from "../api/client";

type ReviewType = "Review1" | "Review2" | "Review3";
type ReviewSessionStatus = "Draft" | "Published" | "Cancelled";

type ScheduleItem = {
  sessionId: number;
  sessionCode: string;
  type: ReviewType;
  sessionDate: string;
  slot: number;
  room: string;
  status: ReviewSessionStatus;
};

type AttendanceItem = {
  sessionId: number;
  sessionCode: string;
  isPresent: boolean;
  note: string | null;
  markedAt: string;
};

type CommentItem = {
  id: number;
  authorUserId: number;
  authorName: string;
  content: string;
  createdAt: string;
  updatedAt: string | null;
};

const slotTimes: Record<number, string> = {
  1: "09:00 – 10:30",
  2: "10:45 – 12:15",
  3: "13:30 – 15:00",
  4: "15:15 – 16:45",
  5: "18:00 – 19:30",
};

export function StudentSchedulePage() {
  const [schedule, setSchedule] = useState<ScheduleItem[]>([]);
  const [attendance, setAttendance] = useState<AttendanceItem[]>([]);
  const [comments, setComments] = useState<CommentItem[]>([]);
  const [selectedSessionId, setSelectedSessionId] = useState<number | null>(null);
  const [message, setMessage] = useState("Đang tải lịch review...");
  const [tab, setTab] = useState<"schedule" | "details">("schedule");

  useEffect(() => {
    void loadData();
  }, []);

  useEffect(() => {
    if (selectedSessionId) {
      void loadComments(selectedSessionId);
    }
  }, [selectedSessionId]);

  async function loadData() {
    try {
      const [scheduleRes, attendanceRes] = await Promise.all([
        apiClient.get<ScheduleItem[]>("/student-review/schedule"),
        apiClient.get<AttendanceItem[]>("/student-review/attendance"),
      ]);
      setSchedule(scheduleRes.data);
      setAttendance(attendanceRes.data);
      setMessage(scheduleRes.data.length > 0 ? "Lịch review của nhóm bạn" : "Chưa có lịch review nào được publish.");
    } catch (error) {
      setMessage(extractError(error));
    }
  }

  async function loadComments(sessionId: number) {
    try {
      const response = await apiClient.get<CommentItem[]>("/student-review/comments", {
        params: { sessionId },
      });
      setComments(response.data);
    } catch {
      setComments([]);
    }
  }

  function getAttendance(sessionId: number) {
    return attendance.find((a) => a.sessionId === sessionId);
  }

  function selectSession(sessionId: number) {
    setSelectedSessionId(sessionId);
    setTab("details");
  }

  const selectedSession = schedule.find((s) => s.sessionId === selectedSessionId);

  return (
    <section className="page student-schedule-page">
      <div className="page-title">
        <div>
          <h2>Lịch review</h2>
          <p>Xem lịch review đã publish, trạng thái điểm danh, và comments từ giảng viên.</p>
        </div>
        <span className="badge">{message}</span>
      </div>

      <div className="segmented-tabs" style={{ gridTemplateColumns: "repeat(2, minmax(130px, 1fr))" }}>
        <button className={tab === "schedule" ? "active" : ""} onClick={() => setTab("schedule")}>Lịch review</button>
        <button className={tab === "details" ? "active" : ""} onClick={() => setTab("details")}>Chi tiết</button>
      </div>

      {tab === "schedule" && (
        <article className="panel table-panel">
          <div className="panel-header">
            <h3>Lịch review của nhóm</h3>
            <button className="secondary" onClick={loadData}>Refresh</button>
          </div>
          <table>
            <thead>
              <tr>
                <th>Session</th>
                <th>Loại</th>
                <th>Ngày</th>
                <th>Slot</th>
                <th>Phòng</th>
                <th>Điểm danh</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {schedule.length === 0 && (
                <tr><td colSpan={7} className="muted">Chưa có lịch review nào.</td></tr>
              )}
              {schedule.map((item) => {
                const att = getAttendance(item.sessionId);
                return (
                  <tr key={item.sessionId}>
                    <td>{item.sessionCode}</td>
                    <td><span className="badge">{item.type}</span></td>
                    <td>{formatDate(item.sessionDate)}</td>
                    <td>Slot {item.slot} ({slotTimes[item.slot] ?? ""})</td>
                    <td><strong>{item.room}</strong></td>
                    <td>
                      {att === undefined && <span className="badge">Chưa điểm danh</span>}
                      {att?.isPresent === true && <span className="tag">✅ Có mặt</span>}
                      {att?.isPresent === false && <span className="live">❌ Vắng mặt</span>}
                    </td>
                    <td><button className="secondary" onClick={() => selectSession(item.sessionId)}>Chi tiết</button></td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </article>
      )}

      {tab === "details" && selectedSession && (
        <article className="panel review-form-panel">
          <div className="panel-header">
            <div>
              <h3>{selectedSession.type} - {selectedSession.sessionCode}</h3>
              <p className="muted">
                {formatDate(selectedSession.sessionDate)} | Slot {selectedSession.slot} ({slotTimes[selectedSession.slot] ?? ""}) | Phòng {selectedSession.room}
              </p>
            </div>
            <button className="secondary" onClick={() => setTab("schedule")}>← Quay lại</button>
          </div>

          {/* Attendance status */}
          {(() => {
            const att = getAttendance(selectedSession.sessionId);
            if (!att) return <div className="alert">Chưa được điểm danh cho session này.</div>;
            return (
              <div className={`alert ${att.isPresent ? "success" : ""}`}>
                <strong>{att.isPresent ? "✅ Có mặt" : "❌ Vắng mặt"}</strong>
                {att.note && <p style={{ margin: "6px 0 0" }}>Ghi chú: {att.note}</p>}
                <small>Cập nhật: {new Date(att.markedAt).toLocaleString()}</small>
              </div>
            );
          })()}

          {/* Comments from lecturers */}
          <div>
            <h4 style={{ marginBottom: 12 }}>Comments từ giảng viên</h4>
            {comments.length === 0 && <p className="muted">Chưa có comment nào.</p>}
            {comments.map((c) => (
              <div className="checklist-item" key={c.id} style={{ marginBottom: 8 }}>
                <div>
                  <strong>{c.authorName}</strong>
                  <small className="muted" style={{ marginLeft: 8 }}>{new Date(c.createdAt).toLocaleString()}</small>
                </div>
                <p style={{ whiteSpace: "pre-wrap" }}>{c.content}</p>
              </div>
            ))}
          </div>
        </article>
      )}

      {tab === "details" && !selectedSession && (
        <article className="panel">
          <p className="muted">Chọn một session từ tab "Lịch review" để xem chi tiết.</p>
        </article>
      )}
    </section>
  );
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString("vi-VN");
}

type ApiErrorBody = { error?: string; title?: string };

function extractError(error: unknown) {
  if (!isAxiosError<ApiErrorBody>(error)) return "Lỗi không xác định.";
  return error.response?.data?.error ?? error.response?.data?.title ?? "Lỗi kết nối server.";
}
