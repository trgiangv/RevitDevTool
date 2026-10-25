import type {JSX} from 'react';

import styles from '../BridgeScene.module.css';

interface CloseButtonProps {
  readonly onClick: () => void;
}

export function CloseButton({onClick}: CloseButtonProps): JSX.Element {
  return (
    <button type="button" className={styles.close} aria-label="Clear selection" onClick={onClick}>
      <svg viewBox="0 0 10 10" width="10" height="10" aria-hidden="true" focusable="false">
        <path d="M1 1l8 8M9 1l-8 8" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
      </svg>
    </button>
  );
}