import useBaseUrl from '@docusaurus/useBaseUrl';
import Codex from '@lobehub/icons/es/Codex';
import {
  siAnaconda,
  siClaude,
  siCondaforge,
  siCursor,
  siGithubcopilot,
  siModelcontextprotocol,
  siNuget,
  siPypi,
  siPytest,
  siUv,
  type SimpleIcon,
} from 'simple-icons';
import {useState, type JSX} from 'react';

import styles from './styles.module.css';

type Entry = {label: string} & ({icon: SimpleIcon} | {src: string} | {render: (size: number) => JSX.Element});

const registry: Record<string, Entry> = {
  nuget: {label: 'NuGet', icon: siNuget},
  pypi: {label: 'PyPI', icon: siPypi},
  'conda-forge': {label: 'conda-forge', icon: siCondaforge},
  anaconda: {label: 'Anaconda', icon: siAnaconda},
  uv: {label: 'uv', icon: siUv},
  python: {label: 'CPython', src: '/icons/python.svg'},
  dotnet: {label: '.NET', src: '/icons/dotnet.svg'},
  csharp: {label: 'C#', src: '/icons/csharp.svg'},
  fsharp: {label: 'F#', src: '/icons/fsharp.svg'},
  pytest: {label: 'pytest', icon: siPytest},
  mcp: {label: 'MCP', icon: siModelcontextprotocol},
  cursor: {label: 'Cursor', icon: siCursor},
  claude: {label: 'Claude', icon: siClaude},
  codex: {label: 'Codex', render: (size) => <Codex.Color size={size} />},
  copilot: {label: 'GitHub Copilot', icon: siGithubcopilot},
  pixi: {label: 'Pixi', src: '/icons/pixi.svg'},
  pyrevit: {label: 'pyRevit', src: '/icons/pyrevit.svg'},
  dynamo: {label: 'Dynamo', src: '/icons/dynamo.png'},
  ironpython: {label: 'IronPython', src: '/icons/ironpython.svg'},
  revit: {label: 'Revit', src: '/icons/revit-icon.svg'},
  autocad: {label: 'AutoCAD', src: '/icons/autocad-icon.svg'},
  civil3d: {label: 'Civil 3D', src: '/icons/civil3d-icon.svg'},
  plant3d: {label: 'Plant 3D', src: '/icons/plant3d-icon.svg'},
  nunit: {label: 'NUnit', src: '/icons/nunit.png'},
  tunit: {label: 'TUnit', src: '/icons/tunit.svg'},
  mstest: {label: 'MSTest', src: '/icons/mstest.svg'},
  vscode: {label: 'VS Code', src: '/icons/vscode.svg'},
};

export type ProductName = keyof typeof registry;

function luminance(hex: string): number {
  const r = Number.parseInt(hex.slice(0, 2), 16);
  const g = Number.parseInt(hex.slice(2, 4), 16);
  const b = Number.parseInt(hex.slice(4, 6), 16);
  return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
}

function Image({src, label, size}: Readonly<{src: string; label: string; size: number}>): JSX.Element {
  const url = useBaseUrl(src);
  const [failed, setFailed] = useState(false);
  return (
    <img
      className={styles.image}
      style={failed ? {display: 'none'} : undefined}
      src={url}
      alt=""
      width={size}
      height={size}
      aria-label={label}
      onError={() => setFailed(true)}
      onLoad={() => setFailed(false)}
    />
  );
}

export default function ProductLogo({name, size = 20}: Readonly<{name: ProductName; size?: number}>): JSX.Element | null {
  const entry = registry[name];
  if (!entry) return null;
  if ('src' in entry) return <Image src={entry.src} label={entry.label} size={size} />;
  if ('render' in entry) return <span className={styles.glyph} role="img" aria-label={entry.label}>{entry.render(size)}</span>;
  const {icon} = entry;
  const lum = luminance(icon.hex);
  const adaptive = lum < 0.15;
  return (
    <svg
      className={styles.glyph}
      data-adaptive={adaptive ? 'true' : undefined}
      data-lift={!adaptive && lum < 0.4 ? 'true' : undefined}
      width={size}
      height={size}
      viewBox="0 0 24 24"
      role="img"
      aria-label={entry.label}
    >
      <path d={icon.path} fill={adaptive ? 'currentColor' : `#${icon.hex}`} />
    </svg>
  );
}

export function LogoRow({items}: Readonly<{items: readonly ProductName[]}>): JSX.Element {
  return (
    <ul className={styles.row}>
      {items.map((name) => {
        const entry = registry[name];
        return (
          <li key={name} className={styles.chip}>
            <ProductLogo name={name} size={18} />
            <span>{entry?.label ?? name}</span>
          </li>
        );
      })}
    </ul>
  );
}
