import { useEffect, useMemo, useState } from "react";
import { apiClient } from "../api/client";
import {
  createDefenseConnection,
  joinDefenseSession,
} from "../api/defenseRealtime";
import type { DefenseSessionState, ScoreSubmittedEvent } from "../api/defenseRealtime";

export function DefensePage() {
  const [sessionId, setSessionId] = useState(1);
  const [studentId, setStudentId] = useState(1);
  const [baoVe, setBaoVe] = useState(8.5);
  const [nguoi, setNguoi] = useState(8);
  const [state, setState] = useState<DefenseSessionState | null>(null);
  const [events, setEvents] = useState<string[]>([
    "Nhập mã phiên/hội đồng, kết nối SignalR, chờ chủ tịch bắt đầu.",
  ]);
  const connection = useMemo(() => createDefenseConnection(), []);

  useEffect(() => {
    connection.on("defenseSessionState", (payload: DefenseSessionState) => {
      setState(payload);
      setEvents((items) => [`Đã tham gia phiên ${payload.sessionId}.`, ...items].slice(0, 6));
    });
    connection.on("defenseSessionStarted", (payload: DefenseSessionState) => {
      setState(payload);
      setEvents((items) => ["Chủ tịch đã bắt đầu phiên chấm.", ...items].slice(0, 6));
    });
    connection.on("defenseSessionClosed", (payload: DefenseSessionState) => {
      setState(payload);
      setEvents((items) => ["Chủ tịch đã kết thúc và khóa điểm.", ...items].slice(0, 6));
    });
    connection.on("scoreSubmitted", (payload: ScoreSubmittedEvent) => {
      setEvents((items) => [
        `Giám khảo ${payload.scorerId} chấm ${payload.scoreType} cho SV ${payload.studentId}: ${payload.scoreValue}.`,
        ...items,
      ].slice(0, 6));
    });
    connection.on("memberJoined", (payload: { fullName: string; code: string }) => {
      setEvents((items) => [`${payload.fullName} (${payload.code}) đã vào phòng chấm.`, ...items].slice(0, 6));
    });

    return () => {
      connection.off("defenseSessionState");
      connection.off("defenseSessionStarted");
      connection.off("defenseSessionClosed");
      connection.off("scoreSubmitted");
      connection.off("memberJoined");
      void connection.stop();
    };
  }, [connection]);

  const isScoringOpen = Boolean(state?.startedAt && !state.isLocked);

  async function joinSession() {
    await joinDefenseSession(connection, sessionId);
  }

  async function startSession() {
    const response = await apiClient.post<DefenseSessionState>(`/defense-sessions/${sessionId}/start`);
    setState(response.data);
  }

  async function closeSession() {
    const response = await apiClient.post<DefenseSessionState>(`/defense-sessions/${sessionId}/close`);
    setState(response.data);
  }

  async function submitScore(scoreType: "BaoVe" | "Nguoi", scoreValue: number) {
    await apiClient.post(`/defense-sessions/${sessionId}/scores`, {
      studentId,
      scoreType,
      scoreValue,
    });
  }

  return (
    <section className="page">
      <div className="page-title">
        <div>
          <h2>Defense scoring</h2>
          <p>SignalR realtime: chủ tịch mở phiên, giám khảo mới được chấm, kết thúc là khóa điểm.</p>
        </div>
        <button className="danger" onClick={closeSession} disabled={!isScoringOpen}>
          End and lock session
        </button>
      </div>
      <div className="dashboard-grid">
        <article className="panel">
          <div className="panel-header">
            <h3>Council {state?.councilCode ?? "chưa kết nối"}</h3>
            <span className={isScoringOpen ? "live" : "badge"}>{isScoringOpen ? "LIVE" : "LOCKED"}</span>
          </div>
          <p className="muted">
            Giám khảo đăng nhập bằng mã do Phòng đào tạo cấp. Chủ tịch là người duy nhất được Start/End.
          </p>
          <div className="member-list">
            <label>
              Defense session ID
              <input type="number" min="1" value={sessionId} onChange={(event) => setSessionId(Number(event.target.value))} />
            </label>
            <button className="secondary" onClick={joinSession}>Join SignalR room</button>
            <button className="primary" onClick={startSession}>Chairman start</button>
            <span>Server validation</span>
            <strong>{isScoringOpen ? "Scoring is open" : "Waiting for chairman"}</strong>
          </div>
        </article>
        <article className="panel scoring">
          <h3>Student scoring</h3>
          <label>
            Student ID
            <input type="number" min="1" value={studentId} onChange={(event) => setStudentId(Number(event.target.value))} />
          </label>
          <label>
            ChamBaoVe
            <input type="number" min="0" max="10" step="0.1" value={baoVe} onChange={(event) => setBaoVe(Number(event.target.value))} />
          </label>
          <label>
            ChamNguoi
            <input type="number" min="0" max="10" step="0.1" value={nguoi} onChange={(event) => setNguoi(Number(event.target.value))} />
          </label>
          <button className="primary" disabled={!isScoringOpen} onClick={() => submitScore("BaoVe", baoVe)}>Submit ChamBaoVe</button>
          <button className="secondary" disabled={!isScoringOpen} onClick={() => submitScore("Nguoi", nguoi)}>Submit ChamNguoi</button>
        </article>
        <article className="panel">
          <h3>Realtime audit feed</h3>
          <ul className="event-feed">
            {events.map((item, index) => <li key={`${item}-${index}`}>{item}</li>)}
          </ul>
        </article>
      </div>
    </section>
  );
}
