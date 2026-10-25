import {useCallback, useEffect, useRef} from 'react';

import {overlayKey, type OverlayRegistry} from './scene/overlay';
import {selectedStackIndex} from './state/selection';
import type {Selection} from './types';

const ESCAPE_KEY = 'Escape';

export function useSelectionClose(reset: () => void, overlay: OverlayRegistry, selection: Selection): () => void {
  const selectionRef = useRef(selection);
  selectionRef.current = selection;

  const close = useCallback(() => {
    const index = selectedStackIndex(selectionRef.current);
    reset();
    if (index >= 0) overlay.get(overlayKey.stack(index))?.focus({preventScroll: true});
  }, [overlay, reset]);

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent): void => {
      if (event.key === ESCAPE_KEY && !event.defaultPrevented) close();
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [close]);

  return close;
}
