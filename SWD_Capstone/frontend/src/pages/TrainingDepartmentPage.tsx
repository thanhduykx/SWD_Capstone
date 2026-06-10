import { useCallback, useEffect, useMemo, useState } from "react";
import { apiClient } from "../api/client";
import { useLanguage } from "../i18n/LanguageContext";

type ReviewType = "Review1" | "Review2" | "Review3";
type ReviewSessionStatus = "Draft" | "Published" | "Cancelled";

type Semester = {
  id: number;
  code: string;
  name: string;
  academicYear: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
};

type BoardLecturer = {
  id: number;
  code: string;
  fullName: string;
  department: string;
  email: string;
};

type BoardAvailability = {
  lecturerId: number;
  dayOfWeek: number;
  slot: number;
};

type BoardAvailabilitySubmission = {
  lecturerId: number;
  isSubmitted: boolean;
  submittedAt?: string | null;
  slotCount: number;
};

type BoardGroup = {
  id: number;
  code: string;
  projectName: string;
  supervisorId: number;
  supervisorCode: string;
  studentCount: number;
};

type BoardSession = {
  id: number;
  code: string;
  groupId: number;
  groupCode: string;
  type: ReviewType;
  status: ReviewSessionStatus;
  reviewerIds: number[];
  sessionDate: string;
  dayOfWeek: number;
  slot: number;
  room: string;
};

type SchedulingBoard = {
  semesterId: number;
  reviewType: ReviewType;
  weekStart: string;
  lecturers: BoardLecturer[];
  availability: BoardAvailability[];
  availabilitySubmissions: BoardAvailabilitySubmission[];
  groups: BoardGroup[];
  sessions: BoardSession[];
};

type RandomAssignResponse = {
  totalCandidateGroups: number;
  assignedCount: number;
  sentEmailCount: number;
  failedEmailCount: number;
  unassignedGroups: Array<{
    groupId: number;
    groupCode: string;
    reason: string;
  }>;
  sessions: BoardSession[];
};

type BulkAssignResponse = {
  sentEmailCount: number;
  failedEmailCount: number;
  sessions: BoardSession[];
};

const reviewTypes: ReviewType[] = ["Review1", "Review2", "Review3"];
const days = [
  { label: "Mon", value: 1 },
  { label: "Tue", value: 2 },
  { label: "Wed", value: 3 },
  { label: "Thu", value: 4 },
  { label: "Fri", value: 5 },
  { label: "Sat", value: 6 },
  { label: "Sun", value: 7 },
];
const slots = [
  { value: 1, time: "07:00-09:15" },
  { value: 2, time: "09:30-11:45" },
  { value: 3, time: "12:30-14:45" },
  { value: 4, time: "15:00-17:15" },
  { value: 5, time: "17:30-19:45" },
  { value: 6, time: "20:00-22:15" },
  { value: 7, time: "22:30-00:45" },
  { value: 8, time: "01:00-03:15" },
];

export function TrainingDepartmentPage() {
  const { t } = useLanguage();
  const [resolvedSemester, setResolvedSemester] = useState<Semester | null>(null);
  const [reviewType, setReviewType] = useState<ReviewType>("Review1");
  const [weekStart, setWeekStart] = useState(toDateInput(getMonday(new Date())));
  const [board, setBoard] = useState<SchedulingBoard | null>(null);
  const [message, setMessage] = useState("Ready");
  const [code, setCode] = useState("");
  const [groupId, setGroupId] = useState<number | "">("");
  const [reviewer1Id, setReviewer1Id] = useState<number | "">("");
  const [reviewer2Id, setReviewer2Id] = useState<number | "">("");
  const [sessionDate, setSessionDate] = useState(toDateInput(new Date()));
  const [slot, setSlot] = useState(1);
  const [room, setRoom] = useState("");
  const [reviewersPerSession, setReviewersPerSession] = useState(2);
  const [roomPrefix, setRoomPrefix] = useState("AUTO");
  const [emailSubject, setEmailSubject] = useState("");
  const [emailMessage, setEmailMessage] = useState("");

  const selectedSemesterId = resolvedSemester?.id ?? 0;
  const availabilitySet = useMemo(() => new Set(
    board?.availability.map((item) => `${item.lecturerId}:${item.dayOfWeek}:${item.slot}`) ?? [],
  ), [board]);
  const selectedGroup = board?.groups.find((group) => group.id === Number(groupId));
  const selectedSessionDayOfWeek = getIsoDayOfWeek(sessionDate);
  const availableLecturersForSessionSlot = useMemo(() => (
    board?.lecturers.filter((lecturer) =>
      lecturer.id !== selectedGroup?.supervisorId &&
      availabilitySet.has(`${lecturer.id}:${selectedSessionDayOfWeek}:${slot}`)) ?? []
  ), [availabilitySet, board, selectedGroup?.supervisorId, selectedSessionDayOfWeek, slot]);
  const submissionByLecturer = useMemo(() => new Map(
    board?.availabilitySubmissions.map((item) => [item.lecturerId, item]) ?? [],
  ), [board]);
  const selectedReviewers = [Number(reviewer1Id), Number(reviewer2Id)]
    .filter((id) => Number.isInteger(id) && id > 0);

  useEffect(() => {
    void resolveSemester(weekStart);
  }, [weekStart]);

  const loadBoard = useCallback(async () => {
    const response = await apiClient.get<SchedulingBoard>("/review-scheduling/board", {
      params: { semesterId: selectedSemesterId, reviewType, weekStart },
    });
    setBoard(response.data);
    setMessage(`Loaded ${reviewType} board`);
  }, [reviewType, selectedSemesterId, weekStart]);

  useEffect(() => {
    if (!selectedSemesterId) {
      return;
    }

    void loadBoard();
  }, [loadBoard, selectedSemesterId]);

  async function resolveSemester(nextWeekStart: string) {
    try {
      const response = await apiClient.get<Semester>("/semesters/resolve", {
        params: { date: nextWeekStart },
      });
      setResolvedSemester(response.data);
    } catch {
      setResolvedSemester(null);
      setMessage("Cannot resolve semester for selected week.");
    }
  }

  async function assignSession() {
    if (!selectedSemesterId || !groupId || !reviewer1Id || !code.trim() || !room.trim()) {
      setMessage("Missing code, group, reviewer, or room.");
      return;
    }

    const reviewerIds = Array.from(new Set(selectedReviewers));
    const response = await apiClient.post<BulkAssignResponse>("/review-sessions/bulk-assign", {
      sessions: [{
        code,
        groupId: Number(groupId),
        groupPosition: board?.sessions.length ? board.sessions.length + 1 : 1,
        type: reviewType,
        reviewerIds,
        previousReviewerIds: [],
        slot,
        room,
        sessionDate,
      }],
    });
    setMessage(`Review session assigned. Sent ${response.data.sentEmailCount} email(s), failed ${response.data.failedEmailCount}.`);
    setCode("");
    setRoom("");
    await loadBoard();
  }

  async function randomAssignSessions() {
    if (!selectedSemesterId) {
      setMessage("Cannot random assign before semester is resolved.");
      return;
    }

    const response = await apiClient.post<RandomAssignResponse>("/review-scheduling/random-assign", {
      semesterId: selectedSemesterId,
      reviewType,
      weekStart,
      reviewersPerSession,
      roomPrefix,
      seed: null,
    });
    const unassignedText = response.data.unassignedGroups.length
      ? ` ${response.data.unassignedGroups.length} group(s) not assigned.`
      : "";
    const emailText = ` Sent ${response.data.sentEmailCount} email(s), failed ${response.data.failedEmailCount}.`;
    setMessage(`Random assigned ${response.data.assignedCount}/${response.data.totalCandidateGroups} group(s).${emailText}${unassignedText}`);
    await loadBoard();
  }

  async function publishSchedule() {
    if (!selectedSemesterId) {
      return;
    }

    const response = await apiClient.post<{ publicationId: number; sentEmailCount: number; failedEmailCount: number }>("/review-schedules/publish", {
      semesterId: selectedSemesterId,
      reviewType,
      weekStart,
      subject: emailSubject,
      message: emailMessage,
    });
    setMessage(`Published #${response.data.publicationId}: sent ${response.data.sentEmailCount}, failed ${response.data.failedEmailCount}.`);
    await loadBoard();
  }

  async function exportZip() {
    if (!selectedSemesterId) {
      return;
    }

    const response = await apiClient.get("/review-submissions/export.zip", {
      params: { semesterId: selectedSemesterId, reviewType },
      responseType: "blob",
    });
    const url = window.URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `review_${reviewType}_${selectedSemesterId}.zip`;
    anchor.click();
    window.URL.revokeObjectURL(url);
  }

  return (
    <section className="page training-review-page">
      <div className="page-title">
        <div>
          <h2>{t.trainingTitle}</h2>
          <p>Operate lecturer availability, manual review scheduling, SMTP publication, and Excel exports.</p>
        </div>
        <span className="badge">{message}</span>
      </div>

      <article className="panel">
        <div className="form-row">
          <div className="resolved-semester">
            <small>Semester</small>
            <strong>{resolvedSemester ? `${resolvedSemester.code} - ${resolvedSemester.name}` : "Resolving..."}</strong>
            {resolvedSemester && <span>{formatDate(resolvedSemester.startDate)} - {formatDate(resolvedSemester.endDate)}</span>}
          </div>
          <label>
            Review round
            <select value={reviewType} onChange={(event) => setReviewType(event.target.value as ReviewType)}>
              {reviewTypes.map((item) => <option key={item} value={item}>{item}</option>)}
            </select>
          </label>
          <label>
            Week start
            <input type="date" value={weekStart} onChange={(event) => {
              const nextWeekStart = toDateInput(getMonday(new Date(event.target.value)));
              setWeekStart(nextWeekStart);
              setSessionDate(nextWeekStart);
            }} />
          </label>
          <button className="secondary align-end" onClick={loadBoard} disabled={!selectedSemesterId}>Refresh board</button>
        </div>
      </article>

      <div className="dashboard-grid">
        <article className="panel">
          <h3>Availability heatmap</h3>
          {!board && <p className="muted">Select semester to load availability.</p>}
          {board && (
            <div className="availability-list">
              {board.lecturers.map((lecturer) => (
                <div className="availability-row" key={lecturer.id}>
                  <div>
                    <strong>{lecturer.code}</strong>
                    <span>{lecturer.fullName}</span>
                    <small>{lecturer.email}</small>
                    {submissionByLecturer.get(lecturer.id)?.isSubmitted ? (
                      <small className="tag">Submitted {submissionByLecturer.get(lecturer.id)?.slotCount ?? 0} slot(s)</small>
                    ) : (
                      <small className="muted">Not submitted</small>
                    )}
                  </div>
                  <div className="mini-slot-grid">
                    {days.flatMap((day) => slots.map((slotItem) => {
                      const available = availabilitySet.has(`${lecturer.id}:${day.value}:${slotItem.value}`);
                      return <span className={available ? "mini-slot available" : "mini-slot"} title={formatSlot(slotItem.value)} key={`${day.value}-${slotItem.value}`}>{available ? slotItem.value : ""}</span>;
                    }))}
                  </div>
                </div>
              ))}
            </div>
          )}
        </article>

        <article className="panel">
          <h3>Assign review session</h3>
          <div className="form-grid">
            <div className="form-row compact">
              <label>
                Reviewers/session
                <select value={reviewersPerSession} onChange={(event) => setReviewersPerSession(Number(event.target.value))}>
                  <option value={1}>1 reviewer</option>
                  <option value={2}>2 reviewers</option>
                </select>
              </label>
              <label>
                Room prefix
                <input value={roomPrefix} onChange={(event) => setRoomPrefix(event.target.value)} placeholder="AUTO" />
              </label>
              <button className="primary align-end" onClick={randomAssignSessions} disabled={!board || board.availability.length === 0}>Random assign groups</button>
            </div>
            <label>
              Session code
              <input value={code} onChange={(event) => setCode(event.target.value)} placeholder="VD: R1-D1-S3-G01" />
            </label>
            <label>
              Group
              <select value={groupId} onChange={(event) => setGroupId(event.target.value ? Number(event.target.value) : "")}>
                <option value="">Select group</option>
                {board?.groups.map((group) => <option key={group.id} value={group.id}>{group.code} - {group.projectName} ({group.studentCount} students)</option>)}
              </select>
            </label>
            {selectedGroup && <p className="alert">Supervisor: {selectedGroup.supervisorCode}. Do not assign this lecturer as reviewer.</p>}
            <label>
              Reviewer 1
              <select value={reviewer1Id} onChange={(event) => setReviewer1Id(event.target.value ? Number(event.target.value) : "")}>
                <option value="">Select reviewer</option>
                {availableLecturersForSessionSlot.map((lecturer) => <option key={lecturer.id} value={lecturer.id}>{lecturer.code} - {lecturer.fullName}</option>)}
              </select>
            </label>
            <label>
              Reviewer 2
              <select value={reviewer2Id} onChange={(event) => setReviewer2Id(event.target.value ? Number(event.target.value) : "")}>
                <option value="">Optional reviewer</option>
                {availableLecturersForSessionSlot.map((lecturer) => <option key={lecturer.id} value={lecturer.id}>{lecturer.code} - {lecturer.fullName}</option>)}
              </select>
            </label>
            {board && availableLecturersForSessionSlot.length === 0 && (
              <p className="alert">No lecturer submitted availability for this date and slot.</p>
            )}
            <div className="form-row compact">
              <label>
                Date
                <input type="date" value={sessionDate} onChange={(event) => setSessionDate(event.target.value)} />
              </label>
              <label>
                Slot
                <select value={slot} onChange={(event) => setSlot(Number(event.target.value))}>
                  {slots.map((item) => <option key={item.value} value={item.value}>{formatSlot(item.value)}</option>)}
                </select>
              </label>
              <label>
                Room
                <input value={room} onChange={(event) => setRoom(event.target.value)} placeholder="NVH 609" />
              </label>
            </div>
            <button className="primary" onClick={assignSession} disabled={!board}>Assign session</button>
          </div>
        </article>
      </div>

      <article className="panel table-panel">
        <div className="panel-header">
          <h3>Scheduled sessions</h3>
          <div className="button-row">
            <button className="secondary" onClick={exportZip} disabled={!selectedSemesterId}>Export zip</button>
          </div>
        </div>
        <table>
          <thead>
            <tr><th>Code</th><th>Group</th><th>Date</th><th>Room</th><th>Reviewers</th><th>Status</th></tr>
          </thead>
          <tbody>
            {!board?.sessions.length && <tr><td colSpan={6} className="muted">No sessions scheduled for this week.</td></tr>}
            {board?.sessions.map((session) => (
              <tr key={session.id}>
                <td>{session.code}</td>
                <td>{session.groupCode}</td>
                <td>{formatDate(session.sessionDate)} - {formatSlot(session.slot)}</td>
                <td>{session.room}</td>
                <td>{session.reviewerIds.map((id) => board.lecturers.find((lecturer) => lecturer.id === id)?.code ?? id).join(", ")}</td>
                <td><span className="tag">{session.status}</span></td>
              </tr>
            ))}
          </tbody>
        </table>
      </article>

      <article className="panel">
        <div className="panel-header">
          <h3>Publish by SMTP email</h3>
          <button className="primary" onClick={publishSchedule} disabled={!board?.sessions.length}>Publish schedule</button>
        </div>
        <div className="form-grid">
          <label>
            Email subject
            <input value={emailSubject} onChange={(event) => setEmailSubject(event.target.value)} placeholder={`${reviewType} schedule`} />
          </label>
          <label>
            Moderator message
            <textarea value={emailMessage} onChange={(event) => setEmailMessage(event.target.value)} rows={4} placeholder="Optional note included in every lecturer email" />
          </label>
        </div>
      </article>
    </section>
  );
}

function getMonday(date: Date) {
  const copy = new Date(date);
  const day = copy.getDay();
  const daysFromMonday = day === 0 ? 6 : day - 1;
  copy.setDate(copy.getDate() - daysFromMonday);
  return copy;
}

function toDateInput(date: Date) {
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString();
}

function formatSlot(slot: number) {
  const definition = slots.find((item) => item.value === slot);
  return definition ? `Slot ${slot} (${definition.time})` : `Slot ${slot}`;
}

function getIsoDayOfWeek(dateValue: string) {
  const day = new Date(dateValue).getDay();
  return day === 0 ? 7 : day;
}
