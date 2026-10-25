import Link from '@docusaurus/Link';
import type {JSX} from 'react';

import {docPath} from '../data/docs';
import {HOSTS} from '../data/hosts';
import {BEATS} from '../data/signals';
import {STACKS} from '../data/stacks';
import styles from '../BridgeScene.module.css';

export function StaticOverview(): JSX.Element {
  return (
    <div className={styles.poster}>
      <div className={styles.posterBody}>
        <p>{BEATS[0]?.text}</p>
        <ul className={styles.chips}>
          {HOSTS.map((host) => (
            <li key={host.name}>{host.name}</li>
          ))}
        </ul>
      </div>
      <details className={styles.browse}>
        <summary>Browse docs by tool</summary>
        <div className={styles.browseGrid}>
          {STACKS.map((stack) => (
            <section key={stack.label}>
              <h3>{stack.label}</h3>
              <ul>
                {stack.docs.map((doc) => (
                  <li key={doc.path}>
                    <Link to={docPath(doc)}>{doc.title}</Link>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </div>
      </details>
    </div>
  );
}
