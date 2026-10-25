import clsx from 'clsx';
import type {JSX} from 'react';

import {HOSTS, MORE_HOSTS_LABEL} from '../data/hosts';
import {BEATS, PREVIEW_PILL, SIGNAL_PILLS} from '../data/signals';
import {STACKS} from '../data/stacks';
import {overlayKey, type OverlayRegistry} from '../scene/overlay';
import styles from '../BridgeScene.module.css';
import {DEVTOOLS_ICON, Icon} from './Icon';

interface OverlayProps {
  readonly registry: OverlayRegistry;
}

interface StackBadgesProps extends OverlayProps {
  readonly selectedIndex: number;
  readonly hoveredIndex: number;
  readonly onToggle: (index: number) => void;
  readonly onHover: (index: number) => void;
}

export function StackBadges({registry, selectedIndex, hoveredIndex, onToggle, onHover}: StackBadgesProps): JSX.Element {
  return (
    <>
      {STACKS.map((stack, index) => (
        <button
          key={stack.label}
          ref={registry.ref(overlayKey.stack(index))}
          type="button"
          className={clsx(styles.label, styles.stackLabel, hoveredIndex === index && styles.hot)}
          aria-pressed={selectedIndex === index}
          aria-controls="bridge-stack-panel"
          aria-label={`${stack.label}: show the related docs`}
          onClick={() => onToggle(index)}
          onPointerEnter={() => onHover(index)}
          onPointerLeave={() => onHover(-1)}
          onFocus={() => onHover(index)}
          onBlur={() => onHover(-1)}
        >
          <Icon source={stack.icon} />
          <span>{stack.label}</span>
        </button>
      ))}
    </>
  );
}

interface HostBadgesProps extends OverlayProps {
  readonly focusedIndex: number;
  readonly hoveredIndex: number;
}

export function HostBadges({registry, focusedIndex, hoveredIndex}: HostBadgesProps): JSX.Element {
  return (
    <>
      {HOSTS.map((host, index) => (
        <div
          key={host.name}
          ref={registry.ref(overlayKey.host(index))}
          className={clsx(styles.label, styles.hostLabel, hoveredIndex === index && styles.hot, focusedIndex === index && styles.focus)}
        >
          <Icon source={host.icon} />
          <span>{host.name}</span>
        </div>
      ))}
      <div ref={registry.ref(overlayKey.more)} className={clsx(styles.label, styles.hostLabel, styles.moreLabel)}>
        {MORE_HOSTS_LABEL}
      </div>
    </>
  );
}

export function GateBadge({registry}: OverlayProps): JSX.Element {
  return (
    <div ref={registry.ref(overlayKey.gate)} className={clsx(styles.label, styles.gate)}>
      <span className={styles.gateFrame}>
        <Icon source={DEVTOOLS_ICON} />
      </span>
      <span className={styles.gateTag}>RevitDevTool</span>
    </div>
  );
}

export function SignalPills({registry}: OverlayProps): JSX.Element {
  return (
    <>
      {SIGNAL_PILLS.map((pill, index) => (
        <div key={`${pill.hostIndex}-${pill.start}`} ref={registry.ref(overlayKey.pill(index))} className={clsx(styles.pill, styles[pill.kind])}>
          <b>{pill.tag}</b>
          <span>{pill.text}</span>
        </div>
      ))}
      <div ref={registry.ref(overlayKey.preview)} className={clsx(styles.pill, styles.preview)}>
        <b>{PREVIEW_PILL.tag}</b>
        <span>{PREVIEW_PILL.text}</span>
      </div>
    </>
  );
}

interface CaptionProps {
  readonly beat: number;
}

export function Caption({beat}: CaptionProps): JSX.Element {
  return (
    <div className={styles.bar}>
      <div className={styles.dots} aria-hidden="true">
        {BEATS.map((item, index) => (
          <i key={item.start} aria-current={index === beat} />
        ))}
      </div>
      <div className={styles.caption} aria-live="polite">{BEATS[beat]?.text}</div>
    </div>
  );
}
