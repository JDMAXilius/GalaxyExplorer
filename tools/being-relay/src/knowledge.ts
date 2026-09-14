import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

const field = (yaml: string, key: string) => yaml.match(new RegExp(`^  ${key}: (.*)$`, "m"))?.[1]?.trim() ?? "";

const list = (yaml: string, key: string) =>
  [...(yaml.match(new RegExp(`^    ${key}:\\n((?:    - .*\\n)+)`, "m"))?.[1] ?? "").matchAll(/^    - (.*)$/gm)].map((m) => m[1]);

const stats = (yaml: string) =>
  [...yaml.matchAll(/- Label: (.*)\n\s+Value: (.*)\n\s+Unit: (.*)\n\s+Exponent: (\d+)/g)]
    .map(([, label, value, unit, exp]) => `${label}: ${value}${exp === "0" ? "" : `^${exp}`} ${unit}`.trim());

function assets(dir: string): string[] {
  try {
    return readdirSync(dir).filter((f) => f.endsWith(".asset")).map((f) => readFileSync(join(dir, f), "utf8"));
  } catch {
    return [];
  }
}

export function knowledge(dataDir: string): string {
  const places = [...assets(join(dataDir, "experiences")), ...assets(join(dataDir, "destinations"))]
    .map((y) => `## ${field(y, "DisplayName")} (id: ${field(y, "Id")})\n${field(y, "SecondLine")}\n${list(y, "Paragraphs").join("\n")}`.trim());
  const bodies = [...assets(join(dataDir, "bodies")), ...assets(join(dataDir, "moons"))]
    .map((y) => `## ${field(y, "DisplayName")} (id: ${field(y, "Id")})\n${field(y, "Subtitle")}\n${field(y, "Paragraph")}\n${stats(y).join("; ")}`.trim());
  return `# Places in the simulation\n\n${places.join("\n\n")}\n\n# Bodies\n\n${bodies.join("\n\n")}`;
}
