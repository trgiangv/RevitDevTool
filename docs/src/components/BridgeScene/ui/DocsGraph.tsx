import Link from '@docusaurus/Link';
import useBaseUrl from '@docusaurus/useBaseUrl';
import {useColorMode} from '@docusaurus/theme-common';
import clsx from 'clsx';
import {useMemo, type CSSProperties, type JSX} from 'react';

import {docPath} from '../data/docs';
import type {StackDefinition} from '../types';
import styles from '../BridgeScene.module.css';
import {computeGraphLayout, GRAPH, placeLabel, type GraphLayout} from './graphLayout';

interface DocsGraphProps {
  readonly stack: StackDefinition;
  readonly active: number;
  readonly onActive: (index: number) => void;
}

interface GraphPartProps {
  readonly stack: StackDefinition;
  readonly layout: GraphLayout;
  readonly active: number;
}

const NODE_DELAY_MS = 220;
const NO_ACTIVE = -1;

const stagger = (index: number, extraMs = 0): CSSProperties => ({'--i': index, '--extra': `${extraMs}ms`} as CSSProperties);

function isNodeLit(layout: GraphLayout, active: number, index: number): boolean {
  if (active < 0) return false;
  return index === active || layout.neighbours[active]?.has(index) === true;
}

function isRelationLit(relation: {readonly a: number; readonly b: number}, active: number): boolean {
  return active >= 0 && (relation.a === active || relation.b === active);
}

function GraphEdges({stack, layout, active}: GraphPartProps): JSX.Element {
  const {centerX, centerY} = GRAPH;
  return (
    <>
      {layout.nodes.map((node, k) => (
        <line
          key={`edge-${stack.docs[k]?.path}`}
          className={clsx(styles.edge, k === active && styles.on)}
          x1={centerX} y1={centerY} x2={node.x.toFixed(1)} y2={node.y.toFixed(1)}
          pathLength={1}
          style={stagger(k)}
        />
      ))}
    </>
  );
}

function GraphRelations({layout, active}: Omit<GraphPartProps, 'stack'>): JSX.Element {
  return (
    <>
      {layout.relations.map((relation, k) => (
        <path
          key={`relation-${relation.a}-${relation.b}`}
          className={clsx(styles.relation, isRelationLit(relation, active) && styles.on)}
          d={relation.path}
          pathLength={1}
          style={stagger(layout.nodes.length + k)}
        />
      ))}
    </>
  );
}

interface GraphNodesProps extends GraphPartProps {
  readonly onActive: (index: number) => void;
}

function GraphNodes({stack, layout, active, onActive}: GraphNodesProps): JSX.Element {
  return (
    <>
      {layout.nodes.map((node, k) => {
        const doc = stack.docs[k];
        if (!doc) return null;
        const {anchor, dx, dy} = placeLabel(node);
        return (
          <Link
            key={doc.path}
            to={docPath(doc)}
            tabIndex={-1}
            className={clsx(styles.node, isNodeLit(layout, active, k) && styles.on)}
            style={stagger(k, NODE_DELAY_MS)}
            onMouseEnter={() => onActive(k)}
            onMouseLeave={() => onActive(NO_ACTIVE)}
          >
            <circle cx={node.x.toFixed(1)} cy={node.y.toFixed(1)} r={GRAPH.nodeRadius} />
            <text x={(node.x + dx).toFixed(1)} y={(node.y + dy).toFixed(1)} textAnchor={anchor}>{doc.shortLabel}</text>
            <title>{doc.title}</title>
          </Link>
        );
      })}
    </>
  );
}

function useStackIconUrl(stack: StackDefinition): string {
  const {colorMode} = useColorMode();
  const name = typeof stack.icon === 'string' ? stack.icon : stack.icon[colorMode];
  return useBaseUrl(`/icons/${name}`);
}

export function DocsGraph({stack, active, onActive}: DocsGraphProps): JSX.Element {
  const layout = useMemo(() => computeGraphLayout(stack), [stack]);
  const iconUrl = useStackIconUrl(stack);
  const {centerX, centerY, iconSize, coreRadius} = GRAPH;

  return (
    <svg className={clsx(styles.graph, active >= 0 && styles.hovering)} viewBox={GRAPH.viewBox} aria-hidden="true" focusable="false">
      <GraphEdges stack={stack} layout={layout} active={active} />
      <GraphRelations layout={layout} active={active} />
      <circle className={styles.core} cx={centerX} cy={centerY} r={coreRadius} />
      <image x={centerX - iconSize / 2} y={centerY - iconSize / 2} width={iconSize} height={iconSize} href={iconUrl} />
      <GraphNodes stack={stack} layout={layout} active={active} onActive={onActive} />
    </svg>
  );
}
