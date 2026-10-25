import {useEffect, useState, type RefObject} from 'react';

import {REDUCED_MOTION_QUERY} from './config';

const NEAR_VIEWPORT_MARGIN = '400px';

export function useReducedMotion(): boolean {
  const [reduced, setReduced] = useState(false);
  useEffect(() => {
    const query = window.matchMedia(REDUCED_MOTION_QUERY);
    setReduced(query.matches);
    const update = (event: MediaQueryListEvent): void => setReduced(event.matches);
    query.addEventListener('change', update);
    return () => query.removeEventListener('change', update);
  }, []);
  return reduced;
}

export function useNearViewport(ref: RefObject<Element | null>): boolean {
  const [near, setNear] = useState(false);
  useEffect(() => {
    const element = ref.current;
    if (!element || near) return undefined;
    const observer = new IntersectionObserver(([entry]) => {
      if (entry?.isIntersecting) setNear(true);
    }, {rootMargin: NEAR_VIEWPORT_MARGIN});
    observer.observe(element);
    return () => observer.disconnect();
  }, [ref, near]);
  return near;
}
