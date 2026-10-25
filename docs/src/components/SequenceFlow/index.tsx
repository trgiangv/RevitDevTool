import BrowserOnly from '@docusaurus/BrowserOnly';
import {useColorMode} from '@docusaurus/theme-common';
import {Handle, Position, ReactFlow, type NodeProps} from '@xyflow/react';
import '@xyflow/react/dist/style.css';
import {useCallback, useMemo, useRef, type JSX} from 'react';

import {
  buildEdges,
  buildNodes,
  describeStep,
  diagramSize,
  headerHeight,
  isNote,
  noteHeight,
  palette,
  rowCenter,
  type LaneData,
  type NoteData,
  type Participant,
  type Step,
} from './model';
import styles from './styles.module.css';
import {useInViewport, useRevealedSteps} from './usePlayback';

export type {Step};

type Props = Readonly<{participants: Participant[]; steps: Step[]}>;

const VISIBLE_THRESHOLD = 0.4;

function LaneNode({data}: NodeProps): JSX.Element {
  const lane = data as LaneData;
  const handleStyle = (index: number) => ({top: rowCenter(index), left: '50%', transform: 'translate(-50%, -50%)'});
  const rows = Array.from({length: lane.rows}, (_, index) => ({index, id: `row-${index}`}));
  return (
    <div className={styles.lane} style={{height: lane.height}} data-active={lane.active ? 'true' : undefined}>
      <div className={styles.card}>
        <span className={styles.title}>{lane.title}</span>
        {lane.subtitle && <span className={styles.subtitle}>{lane.subtitle}</span>}
      </div>
      <div className={styles.lifeline} style={{top: headerHeight - 4}} />
      {rows.map((row) => (
        <span key={row.id}>
          <Handle type="source" position={Position.Right} id={`s${row.index}`} style={handleStyle(row.index)} className={styles.handle} />
          <Handle type="target" position={Position.Left} id={`t${row.index}`} style={handleStyle(row.index)} className={styles.handle} />
        </span>
      ))}
    </div>
  );
}

function NoteNode({data}: NodeProps): JSX.Element {
  const note = data as NoteData;
  return <div className={styles.note} style={{width: note.width, height: noteHeight}}>{note.text}</div>;
}

const nodeTypes = {lane: LaneNode, note: NoteNode};

function Diagram({participants, steps}: Props): JSX.Element {
  const {colorMode} = useColorMode();
  const colors = palette[colorMode === 'dark' ? 'dark' : 'light'];
  const container = useRef<HTMLElement>(null);
  const running = useInViewport(container, VISIBLE_THRESHOLD);
  const revealed = useRevealedSteps(steps.length, running);

  const column = useCallback((id: string) => participants.findIndex((p) => p.id === id), [participants]);
  const currentIndex = running ? revealed - 1 : -1;
  const current = currentIndex >= 0 ? steps[currentIndex] : undefined;
  const activeLane = current && !isNote(current) ? column(current.to) : -1;
  const {width, height} = diagramSize(participants.length, steps.length);

  const nodes = useMemo(
    () => buildNodes({participants, steps, revealed, column, height, activeLane}),
    [participants, steps, revealed, column, height, activeLane],
  );
  const edges = useMemo(
    () => buildEdges({participants, steps, revealed, column, currentIndex, colors}),
    [participants, steps, revealed, column, currentIndex, colors],
  );

  return (
    <figure ref={container} className={styles.flow}>
      <div className={styles.canvas} style={{aspectRatio: `${width} / ${height}`}}>
        <ReactFlow
          nodes={nodes}
          edges={edges}
          nodeTypes={nodeTypes}
          colorMode={colorMode}
          fitView
          fitViewOptions={{padding: 0.02, maxZoom: 3}}
          minZoom={0.2}
          maxZoom={3}
          nodesDraggable={false}
          nodesConnectable={false}
          elementsSelectable={false}
          panOnDrag={false}
          zoomOnScroll={false}
          zoomOnPinch={false}
          zoomOnDoubleClick={false}
          preventScrolling={false}
          proOptions={{hideAttribution: false}}
        />
      </div>
      <figcaption className={styles.footer}>
        <span className={styles.caption} aria-live="off">
          {current ? <><strong>{currentIndex + 1}</strong> / {steps.length} — {describeStep(current)}</> : 'Flow of one run'}
        </span>
      </figcaption>
    </figure>
  );
}

export default function SequenceFlow(props: Props): JSX.Element {
  return <BrowserOnly fallback={<div className={styles.placeholder} />}>{() => <Diagram {...props} />}</BrowserOnly>;
}
