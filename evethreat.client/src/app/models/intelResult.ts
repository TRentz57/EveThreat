export interface IntelResult {
  kills: ZKillTranslatedTop[];
  activeCorp: string;
  activePilots: string[];
  activeTime: string;
  popularShips: string[];
}

export interface ZKillTranslatedTop {
  killmailId: number;
  killmailTime: string;        // ISO date string from backend
  attacker: ZKillTranslated[];
  defender: ZKillTranslated;
  systemName: string;
}

export interface ZKillTranslated {
  characterName?: string | null;
  corpName?: string | null;
  allianceName?: string | null;
  shipName?: string | null;
}