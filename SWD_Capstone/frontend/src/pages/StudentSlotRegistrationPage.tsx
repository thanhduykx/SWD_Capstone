import { useCallback, useEffect, useMemo, useState } from "react";
import { isAxiosError } from "axios";
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

type SlotRegistration = {
  id: number;
  groupId: number;
  groupCode: string;
  dayOfWeek: number;
  slot: number;
};

type SlotGridResponse = {
  roundId: number;
  type: ReviewType;
  status: ReviewRoundStatus;
  weekStartDate: string;
  weekEndDate: string;
  registrations: SlotRegistration[];
  myRegistration: SlotRegistration | null;
};

const days = [
  { label: "Thứ 2", value: 1 },
  { label: "Thứ 3", value: 2 },
  { label: "Thứ 4", value: 3 },
  { label: "Thứ 5", value: 4 },
  { label: "Thứ 6", value: 5 },
  { label: "Thứ 7", value: 6 },
];

const slots = [
  { value: 1, time: "09:00 – 10:30" },
  { value: 2, time: "10:45 – 12:15" },
  { value: 3, time: "13:30 – 15:00" },
  { value: 4, time: "15:15 – 16:45" },
  { value: 5, time: "18:00 – 19:30" },
];

const MAX_GROUPS_PER_SLOT = 3;

const statusBadge: Record<ReviewRoundStatus, { label: string; className: string }> = {
  Draft: { label: "Chưa mở", className: "badge" },
  Open: { label: "Đang mở đăng ký", className: "tag" },
  Closed: { label: "Đã đóng", className: "badge" },
  Published: { label: "Đã publish lịch", className: "tag" },
};

export function StudentSlotRegistrationPage() {
  const [rounds, setRounds] = useState<ReviewRound[]>([]);
  const [selectedRoundId, setSelectedRoundId] = useState<number | null>(null);
  const [grid, setGrid] = useState<SlotGridResponse | null>(null);
  const [message, setMessage] = useState("Chọn đợt review để đăng ký slot.");
  const [isBusy, setIsBusy] = useState(false);

  useEffect(() => {
    void loadRounds();
  }, []);

  const loadRounds = useCallback(async () => {
    try {
      const response = await apiClient.get<ReviewRound[]>("/student-review/rounds");
      setRounds(response.data);
      if (response.data.length > 0 && !selectedRoundId) {
        const openRound = response.data.find((r) => r.status === "Open");
        if (openRound) {
          setSelectedRoundId(openRound.id);
        }
      }
    } catch (error) {
      setMessage(extractError(error));
    }
  }, [selectedRoundId]);

  useEffect(() => {
    if (selectedRoundId) {
      void loadGrid(selectedRoundId);
    }
  }, [selectedRoundId]);

  async function loadGrid(roundId: number) {
    try {
      const response = await apiClient.get<SlotGridResponse>("/student-review/slots", {
        params: { roundId },
      });
      setGrid(response.data);
      if (response.data.myRegistration) {
        setMessage(`Nhóm bạn đã đăng ký slot ${response.data.myRegistration.slot} ngày ${days[response.data.myRegistration.dayOfWeek - 1]?.label ?? ""}.`);
      } else if (response.data.status === "Open") {
        setMessage("Chọn một ô trống để đăng ký slot review.");
      } else {
        setMessage(`Đợt review đang ở trạng thái: ${statusBadge[response.data.status]?.label ?? response.data.status}`);
      }
    } catch (error) {
      setMessage(extractError(error));
    }
  }

  const registrationsBySlot = useMemo(() => {
    const map = new Map<string, SlotRegistration[]>();
    for (const reg of grid?.registrations ?? []) {
      const key = `${reg.dayOfWeek}:${reg.slot}`;
      const list = map.get(key) ?? [];
      list.push(reg);
      map.set(key, list);
    }
    return map;
  }, [grid]);

  async function handleRegister(dayOfWeek: number, slot: number) {
    if (!selectedRoundId || isBusy) return;

    setIsBusy(true);
    try {
      await apiClient.post("/student-review/register", {
        roundId: selectedRoundId,
        dayOfWeek,
        slot,
      });
      setMessage(`Đăng ký thành công! Slot ${slot} - ${days[dayOfWeek - 1]?.label ?? ""}`);
      await loadGrid(selectedRoundId);
      await loadRounds();
    } catch (error) {
      setMessage(extractError(error));
    } finally {
      setIsBusy(false);
    }
  }

  async function handleUnregister() {
    if (!selectedRoundId || isBusy) return;

    setIsBusy(true);
    try {
      await apiClient.delete("/student-review/register", {
        params: { roundId: selectedRoundId },
      });
      setMessage("Đã hủy đăng ký slot.");
      await loadGrid(selectedRoundId);
      await loadRounds();
    } catch (error) {
      setMessage(extractError(error));
    } finally {
      setIsBusy(false);
    }
  }

  const isOpen = grid?.status === "Open";
  const myReg = grid?.myRegistration;

  return (
    <section className="page student-slot-page">
      <div className="page-title">
        <div>
          <h2>Đăng ký slot review</h2>
          <p>Trưởng nhóm chọn slot trống để đăng ký review. Mỗi slot tối đa 3 nhóm review lần lượt.</p>
        </div>
        <span className="badge">{message}</span>
      </div>

      {/* Round selector */}
      <article className="panel">
        <div className="form-row compact">
          <label>
            Đợt review
            <select
              value={selectedRoundId ?? ""}
              onChange={(e) => setSelectedRoundId(e.target.value ? Number(e.target.value) : null)}
            >
              <option value="">-- Chọn đợt --</option>
              {rounds.map((round) => (
                <option key={round.id} value={round.id}>
                  {round.type} | {formatDateOnly(round.weekStartDate)} – {formatDateOnly(round.weekEndDate)} | {statusBadge[round.status]?.label ?? round.status}
                </option>
              ))}
            </select>
          </label>
          {myReg && isOpen && (
            <button className="danger align-end" onClick={handleUnregister} disabled={isBusy}>
              {isBusy ? "Đang hủy..." : "Hủy đăng ký"}
            </button>
          )}
        </div>
      </article>

      {/* My registration badge */}
      {myReg && (
        <div className="alert success">
          ✅ Nhóm <strong>{myReg.groupCode}</strong> đã đăng ký: <strong>{days[myReg.dayOfWeek - 1]?.label ?? ""}</strong> - <strong>Slot {myReg.slot} ({slots[myReg.slot - 1]?.time ?? ""})</strong>
        </div>
      )}

      {/* 30 cell grid */}
      {grid && (
        <div className="slot-grid-30">
          <span className="slot-corner"><small>Slot / Ngày</small></span>
          {days.map((day) => (
            <strong key={day.value} className="slot-header-day">
              <span>{day.label}</span>
              <small>{getDayDate(grid.weekStartDate, day.value)}</small>
            </strong>
          ))}

          {slots.map((slot) => (
            <div className="slot-row-fragment" key={slot.value}>
              <strong className="slot-header-time">
                <span>Slot {slot.value}</span>
                <small>{slot.time}</small>
              </strong>
              {days.map((day) => {
                const key = `${day.value}:${slot.value}`;
                const regs = registrationsBySlot.get(key) ?? [];
                const isMine = myReg?.dayOfWeek === day.value && myReg?.slot === slot.value;
                const isFull = regs.length >= MAX_GROUPS_PER_SLOT;
                const canRegister = isOpen && !myReg && !isFull;

                return (
                  <button
                    key={day.value}
                    className={`slot-cell-30 ${isMine ? "mine" : ""} ${isFull ? "full" : ""} ${regs.length > 0 && !isMine ? "occupied" : ""}`}
                    onClick={() => canRegister && handleRegister(day.value, slot.value)}
                    disabled={!canRegister || isBusy}
                    type="button"
                    title={isFull ? "Slot đầy" : isMine ? "Slot của bạn" : canRegister ? "Click để đăng ký" : ""}
                  >
                    <span className="slot-count">{regs.length}/{MAX_GROUPS_PER_SLOT}</span>
                    {regs.map((r) => (
                      <small key={r.id} className={r.groupId === myReg?.groupId ? "my-group" : ""}>
                        {r.groupCode}
                      </small>
                    ))}
                    {regs.length === 0 && <small className="empty-label">Trống</small>}
                  </button>
                );
              })}
            </div>
          ))}
        </div>
      )}

      {/* Legend */}
      <div className="slot-legend">
        <span className="legend-item"><span className="legend-swatch mine" /> Slot của nhóm bạn</span>
        <span className="legend-item"><span className="legend-swatch occupied" /> Đã có nhóm đăng ký</span>
        <span className="legend-item"><span className="legend-swatch full" /> Đã đầy (3/3)</span>
        <span className="legend-item"><span className="legend-swatch empty" /> Trống</span>
      </div>
    </section>
  );
}

function formatDateOnly(value: string) {
  const parts = value.split("-");
  return `${parts[2]}/${parts[1]}`;
}

function getDayDate(weekStart: string, dayOfWeek: number) {
  const base = new Date(weekStart + "T00:00:00");
  base.setDate(base.getDate() + (dayOfWeek - 1));
  return `${base.getDate()}/${base.getMonth() + 1}`;
}

type ApiErrorBody = { error?: string; title?: string };

function extractError(error: unknown) {
  if (!isAxiosError<ApiErrorBody>(error)) return "Lỗi không xác định.";
  if (error.response?.status === 401) return "Phiên đăng nhập hết hạn.";
  if (error.response?.status === 403) return "Bạn không có quyền thực hiện thao tác này.";
  return error.response?.data?.error ?? error.response?.data?.title ?? "Lỗi kết nối server.";
}
