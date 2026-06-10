import { useCallback, useEffect, useMemo, useState } from "react";
import { isAxiosError } from "axios";
import { apiClient } from "../api/client";
import type { ChangeEvent } from "react";

type ReviewType = "Review1" | "Review2" | "Review3";
type ReviewSessionStatus = "Draft" | "Published" | "Cancelled";
type ReviewSubmissionStatus = "Draft" | "Submitted";
type ReviewChecklistAnswer = "Yes" | "No" | "NotApplicable";

type Semester = {
  id: number;
  code: string;
  name: string;
  academicYear: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
};

type AvailabilitySlot = {
  dayOfWeek: number;
  slot: number;
};

type AvailabilityWeek = {
  semesterId: number;
  weekStart: string;
  isSubmitted: boolean;
  submittedAt?: string | null;
  slots: AvailabilitySlot[];
};

type MyReviewSession = {
  sessionId: number;
  submissionId: number;
  code: string;
  type: ReviewType;
  sessionStatus: ReviewSessionStatus;
  groupCode: string;
  sessionDate: string;
  slot: number;
  room: string;
  submissionStatus: ReviewSubmissionStatus;
  lastSavedAt: string;
};

type ReviewSubmissionItem = {
  itemKey: string;
  label: string;
  description?: string | null;
  priority?: string | null;
  isSection: boolean;
  criteriaCode?: string | null;
  answer?: ReviewChecklistAnswer | null;
  comment?: string | null;
};

type ReviewSubmission = {
  id: number;
  sessionId: number;
  groupCode: string;
  projectName: string;
  type: ReviewType;
  status: ReviewSubmissionStatus;
  sessionStatus: ReviewSessionStatus;
  sessionDate: string;
  slot: number;
  room: string;
  reviewerCode: string;
  reviewerName: string;
  workProductVersion?: string | null;
  workProductSize?: string | null;
  effortHours?: number | null;
  reviewerComment?: string | null;
  suggestion?: string | null;
  resultText?: string | null;
  lastSavedAt: string;
  submittedAt?: string | null;
  items: ReviewSubmissionItem[];
};

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
const currentYear = new Date().getFullYear();
const yearOptions = [currentYear - 1, currentYear, currentYear + 1];

export function ReviewsPage() {
  const [tab, setTab] = useState<"availability" | "schedule" | "form">("availability");
  const [resolvedSemester, setResolvedSemester] = useState<Semester | null>(null);
  const [selectedYear, setSelectedYear] = useState(currentYear);
  const [weekStart, setWeekStart] = useState(toDateInput(getMonday(new Date())));
  const [availability, setAvailability] = useState<AvailabilitySlot[]>([]);
  const [sessions, setSessions] = useState<MyReviewSession[]>([]);
  const [selectedSubmissionId, setSelectedSubmissionId] = useState<number | null>(null);
  const [submission, setSubmission] = useState<ReviewSubmission | null>(null);
  const [dirty, setDirty] = useState(false);
  const [statusMessage, setStatusMessage] = useState("Ready");
  const [isSaving, setIsSaving] = useState(false);

  const selectedSemesterId = resolvedSemester?.id ?? 0;
  const selectedSlotKeys = useMemo(
    () => new Set(availability.map((item) => slotKey(item.dayOfWeek, item.slot))),
    [availability],
  );
  const weekOptions = useMemo(() => getWeekOptions(selectedYear), [selectedYear]);
  const weekDays = useMemo(() => getWeekDays(weekStart), [weekStart]);
  const publishedSessions = sessions.filter((item) => item.sessionStatus === "Published");

  const loadInitialData = useCallback(async () => {
    try {
      const [semesterResponse, sessionResponse] = await Promise.all([
        apiClient.get<Semester>("/semesters/resolve", { params: { date: weekStart } }),
        apiClient.get<MyReviewSession[]>("/review-sessions/my"),
      ]);
      setResolvedSemester(semesterResponse.data);
      setSessions(sessionResponse.data);
    } catch (error) {
      setStatusMessage(getAvailabilityError(error));
    }
  }, [weekStart]);

  useEffect(() => {
    void loadInitialData();
  }, [loadInitialData]);

  useEffect(() => {
    void resolveSemester(weekStart);
  }, [weekStart]);

  useEffect(() => {
    if (!selectedSemesterId) {
      return;
    }

    void loadAvailability(selectedSemesterId, weekStart);
  }, [selectedSemesterId, weekStart]);

  useEffect(() => {
    if (!selectedSubmissionId) {
      setSubmission(null);
      return;
    }

    void loadSubmission(selectedSubmissionId);
  }, [selectedSubmissionId]);

  const saveDraft = useCallback(async () => {
    if (!submission || !dirty || isSaving) {
      return;
    }

    setIsSaving(true);
    try {
      const response = await apiClient.put<ReviewSubmission>(`/review-submissions/${submission.id}/draft`, {
        workProductVersion: submission.workProductVersion ?? "",
        workProductSize: submission.workProductSize ?? "",
        effortHours: submission.effortHours ?? null,
        reviewerComment: submission.reviewerComment ?? "",
        suggestion: submission.suggestion ?? "",
        resultText: submission.resultText ?? "",
        items: submission.items
          .filter((item) => !item.isSection)
          .map((item) => ({
            itemKey: item.itemKey,
            answer: item.answer ?? null,
            comment: item.comment ?? "",
          })),
      });
      setSubmission(response.data);
      setDirty(false);
      setStatusMessage(`Saved at ${formatDateTime(response.data.lastSavedAt)}`);
    } catch {
      setStatusMessage("Autosave failed. Check API/backend.");
    } finally {
      setIsSaving(false);
    }
  }, [dirty, isSaving, submission]);

  useEffect(() => {
    const timer = window.setInterval(() => {
      void saveDraft();
    }, 60_000);

    return () => window.clearInterval(timer);
  }, [saveDraft]);

  async function resolveSemester(nextWeekStart: string) {
    try {
      const response = await apiClient.get<Semester>("/semesters/resolve", {
        params: { date: nextWeekStart },
      });
      setResolvedSemester(response.data);
    } catch (error) {
      setResolvedSemester(null);
      setStatusMessage(getAvailabilityError(error));
    }
  }

  async function loadAvailability(nextSemesterId: number, nextWeekStart: string) {
    try {
      const response = await apiClient.get<AvailabilityWeek>("/review-availability/week", {
        params: { semesterId: nextSemesterId, weekStart: nextWeekStart },
      });
      setAvailability(response.data.slots);
      setStatusMessage(response.data.isSubmitted
        ? `Availability submitted to moderator at ${formatDateTime(response.data.submittedAt ?? new Date().toISOString())}.`
        : "Availability is draft. Save slots, then submit to moderator.");
    } catch (error) {
      setAvailability([]);
      setStatusMessage(getAvailabilityError(error));
    }
  }

  async function saveAvailability() {
    if (!selectedSemesterId) {
      setStatusMessage("He thong chua resolve duoc hoc ky cho tuan nay.");
      return;
    }

    try {
      const response = await apiClient.put<AvailabilityWeek>("/review-availability/week", {
        slots: availability,
      }, {
        params: { semesterId: selectedSemesterId, weekStart },
      });
      setAvailability(response.data.slots);
      setStatusMessage(`Draft availability saved for ${response.data.weekStart}. Submit it when ready for moderator scheduling.`);
    } catch (error) {
      setStatusMessage(getAvailabilityError(error));
    }
  }

  async function submitAvailability() {
    if (!selectedSemesterId) {
      setStatusMessage("He thong chua resolve duoc hoc ky cho tuan nay.");
      return;
    }

    try {
      const response = await apiClient.post<AvailabilityWeek>("/review-availability/week/submit", null, {
        params: { semesterId: selectedSemesterId, weekStart },
      });
      setAvailability(response.data.slots);
      setStatusMessage(`Submitted ${response.data.slots.length} slot(s) to moderator at ${formatDateTime(response.data.submittedAt ?? new Date().toISOString())}.`);
    } catch (error) {
      setStatusMessage(getAvailabilityError(error));
    }
  }

  function changeYear(nextYear: number) {
    setSelectedYear(nextYear);
    setWeekStart(getDefaultWeekStartForYear(nextYear));
  }

  async function loadSubmission(submissionId: number) {
    const response = await apiClient.get<ReviewSubmission>(`/review-submissions/${submissionId}`);
    setSubmission(response.data);
    setDirty(false);
    setStatusMessage(`Loaded ${response.data.groupCode}`);
    setTab("form");
  }

  async function submitReview() {
    if (!submission) {
      return;
    }

    if (dirty) {
      await saveDraft();
    }

    const response = await apiClient.post<ReviewSubmission>(`/review-submissions/${submission.id}/submit`);
    setSubmission(response.data);
    setDirty(false);
    setStatusMessage(`Submitted at ${formatDateTime(response.data.submittedAt ?? response.data.lastSavedAt)}`);
    await loadInitialData();
  }

  async function downloadExcel() {
    if (!submission) {
      return;
    }

    const response = await apiClient.get(`/review-submissions/${submission.id}/export.xlsx`, {
      responseType: "blob",
    });
    const url = window.URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `${submission.groupCode}_${submission.type}.xlsx`;
    anchor.click();
    window.URL.revokeObjectURL(url);
  }

  function toggleAvailability(dayOfWeek: number, slot: number) {
    const key = slotKey(dayOfWeek, slot);
    setAvailability((items) => (
      items.some((item) => slotKey(item.dayOfWeek, item.slot) === key)
        ? items.filter((item) => slotKey(item.dayOfWeek, item.slot) !== key)
        : [...items, { dayOfWeek, slot }]
    ));
  }

  function updateSubmissionField<K extends keyof ReviewSubmission>(field: K, value: ReviewSubmission[K]) {
    setSubmission((current) => current ? { ...current, [field]: value } : current);
    setDirty(true);
  }

  function updateItem(itemKey: string, patch: Partial<ReviewSubmissionItem>) {
    setSubmission((current) => {
      if (!current) {
        return current;
      }

      return {
        ...current,
        items: current.items.map((item) => item.itemKey === itemKey ? { ...item, ...patch } : item),
      };
    });
    setDirty(true);
  }

  return (
    <section className="page review-workspace">
      <div className="page-title">
        <div>
          <h2>Review workflow</h2>
          <p>Register availability, view published assignments, and autosave checklist comments every minute.</p>
        </div>
        <span className="badge">{statusMessage}</span>
      </div>

      <div className="segmented-tabs">
        <button className={tab === "availability" ? "active" : ""} onClick={() => setTab("availability")}>Dang ky slot</button>
        <button className={tab === "schedule" ? "active" : ""} onClick={() => setTab("schedule")}>Lich cua toi</button>
        <button className={tab === "form" ? "active" : ""} onClick={() => setTab("form")}>Form review</button>
      </div>

      {tab === "availability" && (
        <section className="availability-surface">
          <div className="availability-toolbar">
            <div>
              <h3>Dang ky lich trong</h3>
              <p className="muted">Chon slot, save draft, sau do submit de Moderator thay va xep lich review.</p>
            </div>
            <div className="button-row">
              <button className="secondary" onClick={saveAvailability} disabled={!selectedSemesterId}>Save draft</button>
              <button className="primary" onClick={submitAvailability} disabled={!selectedSemesterId || availability.length === 0}>Submit to moderator</button>
            </div>
          </div>

          <div className="schedule-picker-row">
            <div className="resolved-semester">
              <small>Semester</small>
              <strong>{resolvedSemester ? `${resolvedSemester.code} - ${resolvedSemester.name}` : "Resolving..."}</strong>
              {resolvedSemester && <span>{formatDate(resolvedSemester.startDate)} - {formatDate(resolvedSemester.endDate)}</span>}
            </div>
            <label>
              Year
              <select value={selectedYear} onChange={(event) => changeYear(Number(event.target.value))}>
                {yearOptions.map((year) => <option key={year} value={year}>{year}</option>)}
              </select>
            </label>
            <label>
              Week
              <select value={weekStart} onChange={(event) => setWeekStart(event.target.value)}>
                {weekOptions.map((week) => <option key={week.value} value={week.value}>{week.label}</option>)}
              </select>
            </label>
            <label>
              Week start
              <input type="date" value={weekStart} onChange={(event) => {
                const selectedDate = parseDateInput(event.target.value);
                const nextWeekStart = toDateInput(getMonday(selectedDate));
                setSelectedYear(selectedDate.getFullYear());
                setWeekStart(nextWeekStart);
              }} />
            </label>
          </div>

          <div className="availability-table" aria-label="Review availability slots">
            <span className="slot-corner">
              <small>Slot</small>
            </span>
            {weekDays.map((day) => (
              <strong key={day.value}>
                <span>{day.label}</span>
                <small>{day.dateLabel}</small>
              </strong>
            ))}
            {slots.map((slot) => (
              <div className="slot-row-fragment" key={slot.value}>
                <strong><span>{slot.value}</span><small>{slot.time}</small></strong>
                {weekDays.map((day) => {
                  const checked = selectedSlotKeys.has(slotKey(day.value, slot.value));
                  return (
                    <button
                      key={day.value}
                      className={checked ? "slot-cell selected" : "slot-cell"}
                      onClick={() => toggleAvailability(day.value, slot.value)}
                      type="button"
                    >
                      {checked ? "Available" : "-"}
                    </button>
                  );
                })}
              </div>
            ))}
          </div>
        </section>
      )}

      {tab === "schedule" && (
        <article className="panel table-panel">
          <div className="panel-header">
            <h3>Published review assignments</h3>
            <button className="secondary" onClick={loadInitialData}>Refresh</button>
          </div>
          <table>
            <thead>
              <tr><th>Session</th><th>Round</th><th>Group</th><th>Date</th><th>Room</th><th>Status</th><th></th></tr>
            </thead>
            <tbody>
              {publishedSessions.length === 0 && <tr><td colSpan={7} className="muted">No published review sessions yet.</td></tr>}
              {publishedSessions.map((item) => (
                <tr key={item.submissionId}>
                  <td>{item.code}</td>
                  <td>{item.type}</td>
                  <td>{item.groupCode}</td>
                  <td>{formatDate(item.sessionDate)} - {formatSlot(item.slot)}</td>
                  <td>{item.room}</td>
                  <td><span className="tag">{item.submissionStatus}</span></td>
                  <td><button className="secondary" onClick={() => setSelectedSubmissionId(item.submissionId)}>Open</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </article>
      )}

      {tab === "form" && (
        <article className="panel review-form-panel">
          {!submission && <p className="muted">Select a published review session from "Lich cua toi".</p>}
          {submission && (
            <>
              <div className="panel-header">
                <div>
                  <h3>{submission.type} - {submission.groupCode}</h3>
                  <p className="muted">{submission.projectName} | {formatDate(submission.sessionDate)} | {formatSlot(submission.slot)} | {submission.room}</p>
                </div>
                <div className="button-row">
                  <button className="secondary" onClick={saveDraft} disabled={!dirty || isSaving}>{isSaving ? "Saving..." : "Save"}</button>
                  <button className="primary" onClick={submitReview}>Submit</button>
                  <button className="secondary" onClick={downloadExcel}>Export Excel</button>
                </div>
              </div>

              <div className="form-row">
                {submission.type === "Review2" && (
                  <>
                    <label>
                      Work product version
                      <input value={submission.workProductVersion ?? ""} onChange={(event) => updateSubmissionField("workProductVersion", event.target.value)} />
                    </label>
                    <label>
                      Work product size
                      <input value={submission.workProductSize ?? ""} onChange={(event) => updateSubmissionField("workProductSize", event.target.value)} />
                    </label>
                  </>
                )}
                <label>
                  Effort hours
                  <input type="number" min="0" step="0.25" value={submission.effortHours ?? ""} onChange={(event) => updateSubmissionField("effortHours", numberOrNull(event))} />
                </label>
              </div>

              <div className="checklist-list">
                {submission.items.map((item) => item.isSection ? (
                  <h4 className="checklist-section" key={item.itemKey}>{item.label}</h4>
                ) : (
                  <div className="checklist-item" key={item.itemKey}>
                    <div>
                      <strong>{item.criteriaCode ? `${item.criteriaCode} - ${item.label}` : item.label}</strong>
                      {item.priority && <span className="badge">{item.priority}</span>}
                      {item.description && <p className="muted preline">{item.description}</p>}
                    </div>
                    {submission.type !== "Review3" && (
                      <div className="answer-group">
                        {(["Yes", "No", "NotApplicable"] as ReviewChecklistAnswer[]).map((answer) => (
                          <label key={answer} className="radio-chip">
                            <input
                              type="radio"
                              name={item.itemKey}
                              checked={item.answer === answer}
                              onChange={() => updateItem(item.itemKey, { answer })}
                            />
                            {answer === "NotApplicable" ? "N/A" : answer}
                          </label>
                        ))}
                      </div>
                    )}
                    <textarea
                      value={item.comment ?? ""}
                      onChange={(event) => updateItem(item.itemKey, { comment: event.target.value })}
                      placeholder="Reviewer comment"
                      rows={submission.type === "Review3" ? 4 : 2}
                    />
                  </div>
                ))}
              </div>

              <div className="form-row two-columns">
                <label>
                  Comments
                  <textarea value={submission.reviewerComment ?? ""} onChange={(event) => updateSubmissionField("reviewerComment", event.target.value)} rows={4} />
                </label>
                <label>
                  Suggestion
                  <textarea value={submission.suggestion ?? ""} onChange={(event) => updateSubmissionField("suggestion", event.target.value)} rows={4} />
                </label>
              </div>
            </>
          )}
        </article>
      )}
    </section>
  );
}

function slotKey(dayOfWeek: number, slot: number) {
  return `${dayOfWeek}:${slot}`;
}

function getMonday(date: Date) {
  const copy = new Date(date);
  const day = copy.getDay();
  const daysFromMonday = day === 0 ? 6 : day - 1;
  copy.setDate(copy.getDate() - daysFromMonday);
  return copy;
}

function parseDateInput(value: string) {
  const [year, month, day] = value.split("-").map(Number);
  return new Date(year, month - 1, day);
}

function toDateInput(date: Date) {
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${date.getFullYear()}-${month}-${day}`;
}

function toShortDate(date: Date) {
  const month = `${date.getMonth() + 1}`.padStart(2, "0");
  const day = `${date.getDate()}`.padStart(2, "0");
  return `${day}/${month}`;
}

function getWeekDays(weekStartValue: string) {
  const monday = parseDateInput(weekStartValue);
  return days.map((day, index) => {
    const date = new Date(monday);
    date.setDate(monday.getDate() + index);
    return {
      ...day,
      dateLabel: toShortDate(date),
    };
  });
}

function getWeekOptions(year: number) {
  const firstDay = new Date(year, 0, 1);
  const firstMonday = getMonday(firstDay);
  const weeks: Array<{ value: string; label: string }> = [];

  for (let cursor = new Date(firstMonday); cursor.getFullYear() <= year || weeks.length === 0; cursor.setDate(cursor.getDate() + 7)) {
    const sunday = new Date(cursor);
    sunday.setDate(cursor.getDate() + 6);
    if (cursor.getFullYear() > year && sunday.getFullYear() > year) {
      break;
    }

    weeks.push({
      value: toDateInput(cursor),
      label: `${toShortDate(cursor)} To ${toShortDate(sunday)}`,
    });
  }

  return weeks;
}

function getDefaultWeekStartForYear(year: number) {
  if (year === currentYear) {
    return toDateInput(getMonday(new Date()));
  }

  const firstWeekInYear = getWeekOptions(year).find((week) => week.value.startsWith(`${year}-`));
  return firstWeekInYear?.value ?? getWeekOptions(year)[0]?.value ?? toDateInput(getMonday(new Date(year, 0, 1)));
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString();
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString();
}

function numberOrNull(event: ChangeEvent<HTMLInputElement>) {
  return event.target.value === "" ? null : Number(event.target.value);
}

function formatSlot(slot: number) {
  const definition = slots.find((item) => item.value === slot);
  return definition ? `Slot ${slot} (${definition.time})` : `Slot ${slot}`;
}

type ApiErrorBody = {
  error?: string;
  title?: string;
};

function getAvailabilityError(error: unknown) {
  if (!isAxiosError<ApiErrorBody>(error)) {
    return "Khong save duoc slots. Kiem tra API/backend.";
  }

  if (error.response?.status === 401) {
    return "Dang nhap lai truoc khi save slots.";
  }

  if (error.response?.status === 403) {
    return "Chi account Lecturer moi dang ky duoc lich trong. Moderator xep lich/publish trong Training Department.";
  }

  return error.response?.data?.error ?? error.response?.data?.title ?? "Khong save duoc slots. Kiem tra API/backend.";
}
