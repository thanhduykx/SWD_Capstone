import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { apiClient } from "../api/client";
import {
  createDefenseConnection,
  joinDefenseSession,
} from "../api/defenseRealtime";
import { useLanguage } from "../i18n/LanguageContext";
import type { ChangeEvent, FormEvent } from "react";
import type { DefenseSessionState, ScoreSubmittedEvent } from "../api/defenseRealtime";

type DefenseEvidence = {
  id: number;
  defenseSessionId: number;
  capturedByLecturerId: number;
  fileName: string;
  filePath: string;
  contentType: string;
  fileSize: number;
  note?: string | null;
  capturedAt: string;
};

export function DefensePage() {
  const { t } = useLanguage();
  const [defenseCode, setDefenseCode] = useState("");
  const [resolvedCode, setResolvedCode] = useState("");
  const [studentIdInput, setStudentIdInput] = useState("");
  const [baoVeInput, setBaoVeInput] = useState("");
  const [nguoiInput, setNguoiInput] = useState("");
  const [state, setState] = useState<DefenseSessionState | null>(null);
  const [evidenceNote, setEvidenceNote] = useState("");
  const [evidences, setEvidences] = useState<DefenseEvidence[]>([]);
  const [events, setEvents] = useState<string[]>([t.eventInitial]);
  const [cameraActive, setCameraActive] = useState(false);
  const [joinError, setJoinError] = useState<string | null>(null);
  const [isJoining, setIsJoining] = useState(false);
  const videoRef = useRef<HTMLVideoElement | null>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const connection = useMemo(() => createDefenseConnection(), []);
  const sessionId = state?.sessionId ?? 0;
  const activeSession = state;
  const studentId = Number(studentIdInput);
  const baoVe = Number(baoVeInput);
  const nguoi = Number(nguoiInput);
  const hasJoinedSession = Boolean(activeSession);
  const hasValidStudentId = Number.isInteger(studentId) && studentId > 0;
  const hasValidBaoVe = baoVeInput !== "" && baoVe >= 0 && baoVe <= 10;
  const hasValidNguoi = nguoiInput !== "" && nguoi >= 0 && nguoi <= 10;
  const isScoringOpen = Boolean(state?.startedAt && !state.isLocked);

  const loadEvidences = useCallback(async (targetSessionId = sessionId) => {
    if (!targetSessionId) {
      return;
    }

    const response = await apiClient.get<DefenseEvidence[]>(`/defense-sessions/${targetSessionId}/evidences`);
    setEvidences(response.data);
  }, [sessionId]);

  useEffect(() => {
    if (!hasJoinedSession) {
      setEvents([t.eventInitial]);
    }
  }, [hasJoinedSession, t.eventInitial]);

  useEffect(() => {
    connection.on("defenseSessionState", (payload: DefenseSessionState) => {
      setState(payload);
      setEvents((items) => [`${t.eventJoined} ${payload.councilCode}.`, ...items].slice(0, 6));
      void loadEvidences(payload.sessionId);
    });
    connection.on("defenseSessionStarted", (payload: DefenseSessionState) => {
      setState(payload);
      setEvents((items) => [t.eventStarted, ...items].slice(0, 6));
    });
    connection.on("defenseSessionClosed", (payload: DefenseSessionState) => {
      setState(payload);
      setEvents((items) => [t.eventClosed, ...items].slice(0, 6));
    });
    connection.on("scoreSubmitted", (payload: ScoreSubmittedEvent) => {
      setEvents((items) => [
        `Judge ${payload.scorerId} -> ${payload.scoreType} / SV ${payload.studentId}: ${payload.scoreValue}.`,
        ...items,
      ].slice(0, 6));
    });
    connection.on("memberJoined", (payload: { fullName: string; code: string }) => {
      setEvents((items) => [`${payload.fullName} (${payload.code}) ${t.eventMemberJoined}`, ...items].slice(0, 6));
    });
    connection.on("defenseEvidenceCaptured", (payload: DefenseEvidence) => {
      setEvidences((items) => [payload, ...items.filter((item) => item.id !== payload.id)].slice(0, 8));
      setEvents((items) => [t.eventEvidence, ...items].slice(0, 6));
    });

    return () => {
      connection.off("defenseSessionState");
      connection.off("defenseSessionStarted");
      connection.off("defenseSessionClosed");
      connection.off("scoreSubmitted");
      connection.off("memberJoined");
      connection.off("defenseEvidenceCaptured");
      stopCamera();
      void connection.stop();
    };
  }, [connection, hasJoinedSession, loadEvidences, t]);

  async function handleJoinByCode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setJoinError(null);

    if (!defenseCode.trim()) {
      setJoinError("Defense code is required.");
      return;
    }

    try {
      setIsJoining(true);
      const response = await apiClient.get<DefenseSessionState>(`/defense-sessions/resolve/${encodeURIComponent(defenseCode.trim())}`);
      setResolvedCode(defenseCode.trim());
      setState(response.data);
      await joinDefenseSession(connection, response.data.sessionId);
      await loadEvidences(response.data.sessionId);
    } catch {
      setState(null);
      setResolvedCode("");
      setEvidences([]);
      setJoinError("Defense code is invalid or you are not assigned to this council.");
    } finally {
      setIsJoining(false);
    }
  }

  async function startSession() {
    if (!sessionId) {
      return;
    }

    const response = await apiClient.post<DefenseSessionState>(`/defense-sessions/${sessionId}/start`);
    setState(response.data);
  }

  async function closeSession() {
    if (!sessionId) {
      return;
    }

    const response = await apiClient.post<DefenseSessionState>(`/defense-sessions/${sessionId}/close`);
    setState(response.data);
    stopCamera();
  }

  async function submitScore(scoreType: "BaoVe" | "Nguoi", scoreValue: number) {
    if (!sessionId || !hasValidStudentId || scoreValue < 0 || scoreValue > 10) {
      return;
    }

    await apiClient.post(`/defense-sessions/${sessionId}/scores`, {
      studentId,
      scoreType,
      scoreValue,
    });
  }

  async function startCamera() {
    const stream = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
    streamRef.current = stream;
    if (videoRef.current) {
      videoRef.current.srcObject = stream;
      await videoRef.current.play();
    }
    setCameraActive(true);
  }

  function stopCamera() {
    streamRef.current?.getTracks().forEach((track) => track.stop());
    streamRef.current = null;
    setCameraActive(false);
  }

  async function captureEvidence() {
    if (!videoRef.current) {
      return;
    }

    const canvas = document.createElement("canvas");
    canvas.width = videoRef.current.videoWidth || 1280;
    canvas.height = videoRef.current.videoHeight || 720;
    canvas.getContext("2d")?.drawImage(videoRef.current, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", 0.9));
    if (!blob) {
      return;
    }

    await uploadEvidence(new File([blob], `defense-evidence-${Date.now()}.jpg`, { type: "image/jpeg" }));
  }

  async function uploadEvidence(file: File) {
    if (!sessionId) {
      return;
    }

    const formData = new FormData();
    formData.append("file", file);
    formData.append("note", evidenceNote);
    const response = await apiClient.post<DefenseEvidence>(`/defense-sessions/${sessionId}/evidences`, formData, {
      headers: { "Content-Type": "multipart/form-data" },
    });
    setEvidences((items) => [response.data, ...items.filter((item) => item.id !== response.data.id)].slice(0, 8));
  }

  async function uploadSelectedFile(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    await uploadEvidence(file);
    event.target.value = "";
  }

  return (
    <section className="page defense-page">
      <div className="defense-code-panel panel">
        <form className="defense-code-form" onSubmit={handleJoinByCode}>
          <label>
            {t.council} code
            <input value={defenseCode} onChange={(event) => setDefenseCode(event.target.value)} placeholder="VD: 204" />
          </label>
          <button className="primary" type="submit" disabled={isJoining}>{isJoining ? "Checking..." : t.joinRoom}</button>
          {joinError && <p className="form-error">{joinError}</p>}
        </form>
      </div>

      {!hasJoinedSession && (
        <article className="panel">
          <h2>{t.defenseTitle}</h2>
          <p className="muted">{t.eventInitial}</p>
        </article>
      )}

      {activeSession && (
        <>
          <div className="page-title">
            <div>
              <h2>{t.defenseTitle}</h2>
              <p>{t.defenseSubtitle}</p>
            </div>
            <button className="danger" onClick={closeSession} disabled={!isScoringOpen || !activeSession.isChairman}>
              {t.endLock}
            </button>
          </div>
          <div className="dashboard-grid">
            <article className="panel">
              <div className="panel-header">
                <h3>{t.council} {activeSession.councilCode}</h3>
                <span className={isScoringOpen ? "live" : "badge"}>{isScoringOpen ? t.live : t.locked}</span>
              </div>
              <p className="muted">{t.judgeNote}</p>
              <div className="member-list">
                <span>{t.sessionId}: {activeSession.sessionId}</span>
                <span>Code: {resolvedCode}</span>
                <button className="primary" onClick={startSession} disabled={!activeSession.isChairman || isScoringOpen}>{t.chairmanStart}</button>
                <span>{t.serverValidation}</span>
                <strong>{isScoringOpen ? t.scoringOpen : t.waitingChairman}</strong>
              </div>
            </article>
            <article className="panel scoring">
              <h3>{t.studentScoring}</h3>
              <label>
                {t.studentId}
                <input type="number" min="1" value={studentIdInput} onChange={(event) => setStudentIdInput(event.target.value)} />
              </label>
              <label>
                ChamBaoVe
                <input type="number" min="0" max="10" step="0.1" value={baoVeInput} onChange={(event) => setBaoVeInput(event.target.value)} />
              </label>
              <label>
                ChamNguoi
                <input type="number" min="0" max="10" step="0.1" value={nguoiInput} onChange={(event) => setNguoiInput(event.target.value)} />
              </label>
              <button className="primary" disabled={!isScoringOpen || !hasValidStudentId || !hasValidBaoVe} onClick={() => submitScore("BaoVe", baoVe)}>{t.submitBaoVe}</button>
              <button className="secondary" disabled={!isScoringOpen || !hasValidStudentId || !hasValidNguoi} onClick={() => submitScore("Nguoi", nguoi)}>{t.submitNguoi}</button>
            </article>
            <article className="panel evidence-panel">
              <h3>{t.evidenceTitle}</h3>
              <p className="muted">{t.evidenceRule}</p>
              <label>
                {t.evidenceNote}
                <input value={evidenceNote} placeholder={t.evidencePlaceholder} onChange={(event) => setEvidenceNote(event.target.value)} />
              </label>
              <video ref={videoRef} className="camera-preview" muted playsInline />
              <div className="button-row">
                <button className="secondary" onClick={startCamera} disabled={!isScoringOpen || cameraActive}>{t.startCamera}</button>
                <button className="primary" onClick={captureEvidence} disabled={!isScoringOpen || !cameraActive}>{t.capture}</button>
                <button className="danger" onClick={stopCamera} disabled={!cameraActive}>{t.stopCamera}</button>
              </div>
              <label className="file-upload">
                {t.uploadFile}
                <input type="file" accept="image/*" disabled={!isScoringOpen} onChange={uploadSelectedFile} />
              </label>
              <div className="evidence-grid">
                {evidences.length === 0 && <p className="muted">{t.noEvidence}</p>}
                {evidences.map((item) => (
                  <a key={item.id} href={item.filePath} target="_blank" rel="noreferrer">
                    <img src={item.filePath} alt={item.note ?? item.fileName} />
                    <span>{item.note ?? item.fileName}</span>
                  </a>
                ))}
              </div>
            </article>
            <article className="panel">
              <h3>{t.auditFeed}</h3>
              <ul className="event-feed">
                {events.map((item, index) => <li key={`${item}-${index}`}>{item}</li>)}
              </ul>
            </article>
          </div>
        </>
      )}
    </section>
  );
}
