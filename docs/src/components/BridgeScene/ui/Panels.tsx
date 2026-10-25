import Link from '@docusaurus/Link';
import clsx from 'clsx';
import {useEffect, useRef, useState, type JSX} from 'react';

import {docPath} from '../data/docs';
import type {HostDefinition, StackDefinition} from '../types';
import styles from '../BridgeScene.module.css';
import {CloseButton} from './CloseButton';
import {DocsGraph} from './DocsGraph';
import {Icon} from './Icon';

interface HostPanelProps {
  readonly host: HostDefinition;
  readonly onClose: () => void;
}

export function HostPanel({host, onClose}: HostPanelProps): JSX.Element {
  return (
    <output className={styles.hostPanel}>
      <div className={styles.panelHead}>
        <Icon source={host.icon} />
        <span className={styles.panelName}>{host.name}</span>
        <CloseButton onClick={onClose} />
      </div>
      <ul>
        {host.bullets.map((bullet) => (
          <li key={bullet}>{bullet}</li>
        ))}
      </ul>
      <a href={host.link.href} target="_blank" rel="noopener noreferrer">{host.link.label} ↗</a>
    </output>
  );
}

interface StackPanelProps {
  readonly stack: StackDefinition;
  readonly onClose: () => void;
}

export function StackPanel({stack, onClose}: StackPanelProps): JSX.Element {
  const panelRef = useRef<HTMLElement>(null);
  const [active, setActive] = useState(-1);

  useEffect(() => {
    panelRef.current?.querySelector('button')?.focus({preventScroll: true});
  }, []);

  return (
    <aside ref={panelRef} id="bridge-stack-panel" className={styles.stackPanel} aria-label="Selected stack">
      <div className={styles.stackHead}>
        <Icon source={stack.icon} className={styles.stackIcon} />
        <div className={styles.stackTitle}>{stack.label}</div>
        <CloseButton onClick={onClose} />

      </div>
      <p className={styles.stackText}>{stack.summary}</p>
      <span className={styles.panelLabel}>Related docs</span>
      <DocsGraph stack={stack} active={active} onActive={setActive} />
      <ul className={styles.links}>
        {stack.docs.map((doc, index) => (
          <li key={doc.path}>
            <Link
              to={docPath(doc)}
              className={clsx(index === active && styles.on)}
              onMouseEnter={() => setActive(index)}
              onMouseLeave={() => setActive(-1)}
              onFocus={() => setActive(index)}
              onBlur={() => setActive(-1)}
            >
              {doc.title}
            </Link>
          </li>
        ))}
      </ul>
    </aside>
  );
}
