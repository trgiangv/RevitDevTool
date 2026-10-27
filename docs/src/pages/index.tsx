import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import {Blocks, Bot, Bug, FlaskConical, Package, ScrollText, Spline, type LucideIcon} from 'lucide-react';
import {Highlight, themes} from 'prism-react-renderer';
import type {JSX} from 'react';

import {BridgeScene} from '../components/BridgeScene';

import styles from './index.module.css';

function Hero(): JSX.Element {
  return (
    <header className={styles.hero}>
      <div className={`${styles.container} ${styles.heroTop}`}>
        <div>
          <p className={styles.eyebrow}>Revit · AutoCAD · Civil 3D · Plant 3D · 2022–2027</p>
          <h1 className={styles.heroTitle}>The development platform for BIM and CAD.</h1>
        </div>
        <div className={styles.heroAside}>
          <p>
            Run Python, C# and F# in the live session and test with real breakpoints. Let AI assistants work with
            your model through MCP, and extend it with toolsets of your own.
          </p>
          <div className={styles.actions}>
            <Link className={styles.primaryButton} to="/docs/getting-started/installation">Get started</Link>
            <Link className={styles.textLink} to="/docs/">What is RevitDevTool?</Link>
          </div>
        </div>
      </div>
      <div className={styles.container}><BridgeScene /></div>
    </header>
  );
}

type Token = {types: string[]; content: string};

function Code({code, language}: Readonly<{code: string; language: string}>): JSX.Element {
  return (
    <Highlight code={code} language={language} theme={themes.github}>
      {({tokens, getLineProps, getTokenProps}) => (
        <pre className={styles.code} tabIndex={0}>
          <code>
            {tokens.map((line, lineIndex) => {
              const {style: _lineStyle, ...lineProps} = getLineProps({line});
              return (
                <div key={lineIndex} {...lineProps}>
                  {line.map((token: Token, tokenIndex: number) => {
                    const {style: _tokenStyle, ...tokenProps} = getTokenProps({token});
                    return <span key={tokenIndex} {...tokenProps} />;
                  })}
                </div>
              );
            })}
          </code>
        </pre>
      )}
    </Highlight>
  );
}

const helloScript = `# /// script
# dependencies = ["polars"]
# ///

import polars as pl
from Autodesk.Revit import DB

doc = __revit__.ActiveUIDocument.Document
walls = DB.FilteredElementCollector(doc).OfClass(DB.Wall)
print(pl.DataFrame({"name": [w.Name for w in walls]}))`;

type Claim = {name: string; icon: LucideIcon; text: string; to: string; link: string; code: string; language: string};

const claims: Claim[] = [
  {
    name: 'Install Python packages from the script header',
    icon: Package,
    text: 'Declare dependencies in the script itself. Pixi resolves them from conda-forge and PyPI on first run, into a CPython 3.14 that runs in the host process.',
    to: '/docs/execution/python/Execution-Python',
    link: 'Modern Python scripting',
    language: 'python',
    code: helloScript,
  },
  {
    name: 'Stop at your breakpoints',
    icon: Bug,
    text: 'Attach VS Code or PyCharm to a running CPython or IronPython script and inspect real Revit and AutoCAD API objects. For C#, F# and compiled add-ins, attach your IDE to the host process.',
    to: '/docs/execution/python/Execution-PythonDebugging',
    link: 'Attach a debugger',
    language: 'json',
    code: `{
  "name": "Attach RevitDevTool Python",
  "type": "debugpy",
  "request": "attach",
  "connect": { "host": "localhost", "port": 5678 }
}`,
  },
  {
    name: 'Run your tests in a live session',
    icon: FlaskConical,
    text: 'pytest, NUnit, TUnit and MSTest execute inside a real Revit or AutoCAD process. Results come back to your terminal or IDE.',
    to: '/docs/testing/Testing-Overview',
    link: 'Testing overview',
    language: 'python',
    code: `def test_active_document(revit):
    assert revit.active_document is not None

# uv run pytest --host revit --host-version 2025 -v`,
  },
  {
    name: 'Let AI assistants work with your model',
    icon: Bot,
    text: 'An MCP server connects assistants such as Cursor, Claude, GitHub Copilot and Codex to running hosts. Ask in plain language: the assistant searches for a capability, calls it, and reads back the result.',
    to: '/docs/mcp/MCP-Protocol',
    link: 'MCP protocol',
    language: 'json',
    code: `{
  "mcpServers": {
    "revitdevtool": {
      "type": "stdio",
      "command": "DevTools.Daemon.exe",
      "args": ["--stdio"]
    }
  }
}`,
  },
  {
    name: 'Extend MCP with your own toolsets',
    icon: Blocks,
    text: 'Write tools, resources and prompts as a Python folder or a .NET assembly and register it in Settings → MCP. Every connected assistant can then find and call it, so your own automation becomes something the whole team can ask for.',
    to: '/docs/mcp/MCP-Python',
    link: 'Build a toolset',
    language: 'python',
    code: `@mcp.tool(structured_output=True)
async def revit_get_model_summary() -> ModelSummaryResult:
    """Project overview: categories, warnings, levels."""
    return service.get_model_summary()`,
  },
  {
    name: 'Read color-coded logs and stack traces',
    icon: ScrollText,
    text: 'Output from Trace, print() and Console is colored by level, with JSON and Python stack traces formatted for you, in a dockable panel, a file, or an HTTP endpoint.',
    to: '/docs/logging/Logging-Overview',
    link: 'Log level and output',
    language: 'csharp',
    code: `Trace.TraceInformation("Export started");
Trace.TraceWarning("Room 31 has no boundary");
Trace.TraceError("Export failed: {0}", exception.Message);`,
  },
  {
    name: 'Preview geometry without touching the model',
    icon: Spline,
    text: 'Print a curve, face or solid and it appears in the active Revit view. Nothing is added to the document.',
    to: '/docs/logging/Visualization-Overview',
    link: 'Geometry visualization',
    language: 'python',
    code: `ref = uidoc.Selection.PickObject(ObjectType.Edge)
edge = elem.GetGeometryObjectFromReference(ref)
print(edge)  # drawn in the active view`,
  },
];

function Claims(): JSX.Element {
  return (
    <section className={styles.claims} aria-labelledby="claims-title">
      <div className={styles.container}>
        <h2 id="claims-title" className={styles.sectionTitle}>What you can do with it.</h2>
        <p className={styles.sectionLead}>Open any item to see the code behind it.</p>
        <div className={styles.claimList}>
          {claims.map(({icon: ClaimIcon, ...claim}, index) => (
            <details key={claim.name} className={styles.claim} open={index === 0}>
              <summary>
                <ClaimIcon className={styles.claimIcon} aria-hidden="true" />
                <span className={styles.claimName}>{claim.name}</span>
                <span className={styles.chevron} aria-hidden="true" />
              </summary>
              <div className={styles.claimBody}>
                <div className={styles.claimText}>
                  <p>{claim.text}</p>
                  <Link to={claim.to}>{claim.link}</Link>
                </div>
                <Code code={claim.code} language={claim.language} />
              </div>
            </details>
          ))}
        </div>
      </div>
    </section>
  );
}

function Start(): JSX.Element {
  return (
    <section className={styles.start} aria-labelledby="start-title">
      <div className={`${styles.container} ${styles.startGrid}`}>
        <div>
          <h2 id="start-title" className={styles.sectionTitle}>Run your first script.</h2>
          <ol className={styles.steps}>
            <li>
              <strong>Install.</strong> Download <code>RevitDevTool-Setup.exe</code> from{' '}
              <a href="https://github.com/trgiangv/RevitDevTool/releases/latest">GitHub Releases</a>. It installs for
              the current user, no administrator rights needed.
            </li>
            <li><strong>Write.</strong> Save this as <code>hello_script.py</code> in an empty folder.</li>
          </ol>
          <Code code={helloScript} language="python" />
          <ol className={styles.steps} start={3}>
            <li><strong>Load.</strong> In the panel, turn on <strong>Execution</strong>, choose <strong>Load Scripts</strong> and select the folder.</li>
            <li><strong>Run.</strong> Pick the discovered command and read the output in the log view.</li>
          </ol>
          <Link className={styles.primaryButton} to="/docs/getting-started/installation">Full walkthrough</Link>
        </div>
        <div className={styles.migration}>
          <h3>Already use pyRevit or Add-In Manager?</h3>
          <p>
            Keep them. RevitDevTool works beside them rather than replacing them: use pyRevit for ribbon buttons
            and team distribution, and RevitDevTool for development, debugging and testing. IronPython scripts run on
            the pyRevit engine when it is installed.
          </p>
          <Link className={styles.textLink} to="/docs/execution/python/Execution-PythonEcosystems">Compare Python options</Link>
        </div>
      </div>
    </section>
  );
}

export default function Home(): JSX.Element {
  return (
    <Layout title="The development platform for BIM and CAD" description="Run Python, C# and F# in live Revit and AutoCAD sessions, test in-process, and connect AI assistants through MCP with toolsets you can extend.">
      <main className={styles.page}><Hero /><Claims /><Start /></main>
    </Layout>
  );
}