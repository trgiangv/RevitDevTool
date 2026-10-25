import BrowserOnly from '@docusaurus/BrowserOnly';
import {lazy, Suspense, useRef, type JSX} from 'react';

import styles from './BridgeScene.module.css';
import {useNearViewport} from './hooks';
import {StaticOverview} from './ui/StaticOverview';

const BridgeStage = lazy(() => import('./BridgeStage'));

export function BridgeScene(): JSX.Element {
  const figureRef = useRef<HTMLElement>(null);
  const nearViewport = useNearViewport(figureRef);

  return (
    <figure ref={figureRef} className={styles.scene}>
      <div className={styles.card}>
        {nearViewport ? (
          <BrowserOnly fallback={<StaticOverview />}>
            {() => (
              <Suspense fallback={<StaticOverview />}>
                <BridgeStage />
              </Suspense>
            )}
          </BrowserOnly>
        ) : (
          <StaticOverview />
        )}
      </div>
      <figcaption className={styles.note}>
        The tools you already use stream into your live host sessions, and logs, test results and AI answers come back from the
        same host. Routing and signals are illustrative. In the product, geometry preview currently runs on Revit only.
      </figcaption>
    </figure>
  );
}
