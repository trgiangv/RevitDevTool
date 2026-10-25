import {useEffect, useState, type RefObject} from 'react';

const STEP_MS = 1300;
const HOLD_MS = 2600;
const REDUCED_MOTION_QUERY = '(prefers-reduced-motion: reduce)';

export function useInViewport(element: RefObject<HTMLElement | null>, threshold: number): boolean {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const target = element.current;
    if (!target) return undefined;
    const observer = new IntersectionObserver(
      (entries) => setVisible(entries.some((entry) => entry.isIntersecting)),
      {threshold},
    );
    observer.observe(target);
    return () => observer.disconnect();
  }, [element, threshold]);

  return visible;
}

function loopSteps(count: number, onShow: (shown: number) => void): () => void {
  let shown = 1;
  let handle = 0;

  function schedule(action: () => void, delay: number): void {
    handle = window.setTimeout(action, delay);
  }

  function restart(): void {
    shown = 1;
    onShow(shown);
    schedule(tick, STEP_MS);
  }

  function tick(): void {
    if (shown >= count) {
      schedule(restart, HOLD_MS);
      return;
    }
    shown += 1;
    onShow(shown);
    schedule(tick, STEP_MS);
  }

  onShow(shown);
  schedule(tick, STEP_MS);
  return () => window.clearTimeout(handle);
}

export function useRevealedSteps(count: number, running: boolean): number {
  const [revealed, setRevealed] = useState(count);

  useEffect(() => {
    if (!running || window.matchMedia(REDUCED_MOTION_QUERY).matches) {
      setRevealed(count);
      return undefined;
    }
    return loopSteps(count, setRevealed);
  }, [running, count]);

  return revealed;
}
