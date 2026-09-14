export interface BeingContext {
  place: string;
  placeName: string;
  layout: string;
  pulled: string[];
  platform: string;
}

export interface ClientMessage {
  type: "hello" | "turn" | "interrupt";
  text?: string;
  audioBytes?: number;
  context?: BeingContext;
}

export interface ServerMessage {
  type: "transcript" | "text" | "action" | "done" | "error";
  text?: string;
  name?: string;
  args?: string;
}
