import {useColorMode} from '@docusaurus/theme-common';
import clsx from 'clsx';
import {useMemo, useReducer, useRef, useState, type JSX} from 'react';

import styles from './BridgeScene.module.css';
import {HOSTS} from './data/hosts';
import {STACKS} from './data/stacks';
import {OverlayRegistry} from './scene/overlay';
import {IDLE, selectedHostIndex, selectedStackIndex, selectionReducer} from './state/selection';
import {Caption, GateBadge, HostBadges, SignalPills, StackBadges} from './ui/Badges';
import {HostPanel, StackPanel} from './ui/Panels';
import {StaticOverview} from './ui/StaticOverview';
import {useSceneController} from './useSceneController';
import {useSelectionClose} from './useSelectionClose';

const CANVAS_LABEL =
  'Interactive 3D diagram. Drag and release to spin it, hold Control and scroll to zoom. With a tool or host label focused, arrow keys orbit, plus or minus zoom, and Escape resets the view.';

export default function BridgeStage(): JSX.Element {
  const stageRef = useRef<HTMLDivElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const overlay = useMemo(() => new OverlayRegistry(), []);
  const {colorMode} = useColorMode();
  const [selection, dispatch] = useReducer(selectionReducer, IDLE);
  const [hoveredStack, setHoveredStack] = useState(-1);
  const {beat, hoveredHost, failed, reset} = useSceneController({
    stageRef,
    canvasRef,
    overlay,
    dark: colorMode === 'dark',
    selection,
    hoveredStack,
    dispatch,
  });
  const closeSelection = useSelectionClose(reset, overlay, selection);

  if (failed) return <StaticOverview />;

  const stackIndex = selectedStackIndex(selection);
  const hostIndex = selectedHostIndex(selection);
  const stack = STACKS[stackIndex];
  const host = HOSTS[hostIndex];

  return (
    <>
      <div className={styles.stage}>
        <div ref={stageRef} className={styles.viewport}>
          <canvas ref={canvasRef} className={styles.canvas} aria-label={CANVAS_LABEL} />
          <div className={clsx(styles.overlay, beat >= 0 && styles.ready)}>
            <StackBadges
              registry={overlay}
              selectedIndex={stackIndex}
              hoveredIndex={hoveredStack}
              onToggle={(index) => dispatch({type: 'toggleStack', index})}
              onHover={setHoveredStack}
            />
            <GateBadge registry={overlay} />
            <HostBadges registry={overlay} focusedIndex={hostIndex} hoveredIndex={hoveredHost} />
            <SignalPills registry={overlay} />
          </div>
        </div>
        {host && <HostPanel host={host} onClose={closeSelection} />}
        {stack && <StackPanel key={stack.label} stack={stack} onClose={closeSelection} />}
      </div>
      <Caption beat={Math.max(beat, 0)} />
    </>
  );
}
