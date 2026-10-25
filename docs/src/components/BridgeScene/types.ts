export interface ThemedAsset {
  readonly light: string;
  readonly dark: string;
}

export type IconSource = string | ThemedAsset;

export interface DocPage {
  readonly shortLabel: string;
  readonly title: string;
  readonly path: string;
}

export type RelatedPair = readonly [number, number];

export interface StackDefinition {
  readonly label: string;
  readonly icon: IconSource;
  readonly colors: ThemedAsset;
  readonly hostIndex: number;
  readonly activeWindow: readonly [number, number];
  readonly lane: { readonly y: number; readonly z: number };
  readonly summary: string;
  readonly docs: readonly DocPage[];
  readonly related: readonly RelatedPair[];
}

export interface HostDefinition {
  readonly name: string;
  readonly icon: string;
  readonly y: number;
  readonly bullets: readonly string[];
  readonly link: { readonly label: string; readonly href: string };
}

export type SignalKind = 'inf' | 'dbg' | 'wrn' | 'err' | 'ok' | 'fail' | 'cancel' | 'ai';

export interface SignalPill {
  readonly kind: SignalKind;
  readonly tag: string;
  readonly text: string;
  readonly start: number;
  readonly duration: number;
  readonly hostIndex: number;
  readonly yOffset: number;
  readonly outbound: boolean;
}

export type LaneTone = 'gate' | 'ai' | 'ok';

export interface LaneDefinition {
  readonly hostIndex: number;
  readonly direction: 1 | -1;
  readonly tone: LaneTone;
  readonly activity: readonly [number, number, number, number];
}

export interface Beat {
  readonly start: number;
  readonly text: string;
}

export interface Flash {
  readonly at: number;
  readonly width: number;
}

export type Selection =
  | { readonly kind: 'idle' }
  | { readonly kind: 'stack'; readonly index: number }
  | { readonly kind: 'host'; readonly index: number };
