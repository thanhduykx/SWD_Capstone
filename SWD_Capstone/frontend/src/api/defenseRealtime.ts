import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import type { HubConnection } from "@microsoft/signalr";

export type DefenseSessionState = {
  sessionId: number;
  sessionCode: string;
  defenseRoundId: number;
  councilId: number;
  councilCode: string;
  groupId: number;
  sessionDate: string;
  slot: number;
  room: string;
  startedAt?: string | null;
  endedAt?: string | null;
  isLocked: boolean;
  isChairman: boolean;
};

export type ScoreSubmittedEvent = {
  sessionId: number;
  id: number;
  scorerId: number;
  studentId: number;
  scoreType: "BaoVe" | "Nguoi";
  scoreValue: number;
  submittedAt: string;
};

export function createDefenseConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl("/hubs/defense", {
      accessTokenFactory: () => sessionStorage.getItem("cpms_access_token") ?? "",
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Information)
    .build();
}

export async function joinDefenseSession(connection: HubConnection, sessionId: number) {
  if (connection.state === HubConnectionState.Disconnected) {
    await connection.start();
  }

  await connection.invoke("JoinDefenseSession", sessionId);
}
