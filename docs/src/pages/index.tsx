import Link from '@docusaurus/Link';
import Layout from '@theme/Layout';
import {Highlight, themes} from 'prism-react-renderer';
import {useState, type JSX} from 'react';

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

type Claim = {name: string; text: string; to: string; link: string; code: string; language: string};

const claims: Claim[] = [
  {
    name: 'Installs_python_packages_from_the_script_header',
    text: 'Declare dependencies in the script itself. Pixi resolves them from conda-forge and PyPI on first run, into a CPython 3.14 that runs in the host process.',
    to: '/docs/execution/python/Execution-Python',
    link: 'Modern Python scripting',
    language: 'python',
    code: `# /// script
# dependencies = ["polars", "numpy"]
# ///

import polars as pl
from Autodesk.Revit import DB

doc = __revit__.ActiveUIDocument.Document
walls = DB.FilteredElementCollector(doc).OfClass(DB.Wall)
print(pl.DataFrame({"name": [w.Name for w in walls]}))`,
  },
  {
    name: 'Stops_at_your_breakpoints',
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
    name: 'Runs_your_tests_in_a_live_session',
    text: 'pytest, NUnit, TUnit and MSTest execute inside a real Revit or AutoCAD process. Results come back to your terminal or IDE.',
    to: '/docs/testing/Testing-Overview',
    link: 'Testing overview',
    language: 'python',
    code: `def test_active_document(revit):
    assert revit.active_document is not None

# uv run pytest --host revit --host-version 2025 -v`,
  },
  {
    name: 'Lets_AI_assistants_work_with_your_model',
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
    name: 'Extends_MCP_with_your_own_toolsets',
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
    name: 'Shows_color_coded_logs_and_stack_traces',
    text: 'Output from Trace, print() and Console is colored by level, with JSON and Python stack traces formatted for you, in a dockable panel, a file, or an HTTP endpoint.',
    to: '/docs/logging/Logging-Overview',
    link: 'Log level and output',
    language: 'csharp',
    code: `Trace.TraceInformation("Export started");
Trace.TraceWarning("Room 31 has no boundary");
Trace.TraceError("Export failed: {0}", exception.Message);`,
  },
  {
    name: 'Previews_geometry_without_touching_the_model',
    text: 'Print a curve, face or solid and it appears in the active Revit view. Nothing is added to the document.',
    to: '/docs/logging/Visualization-Overview',
    link: 'Geometry visualization',
    language: 'python',
    code: `ref = uidoc.Selection.PickObject(ObjectType.Edge)
edge = elem.GetGeometryObjectFromReference(ref)
print(edge)  # drawn in the active view`,
  },
];

function TestName({name}: Readonly<{name: string}>): JSX.Element {
  const words = name.split('_');
  let offset = 0;
  const parts = words.map((word, position) => {
    const part = {word, offset, last: position === words.length - 1};
    offset += word.length + 1;
    return part;
  });
  return <>{parts.map((part) => <span key={part.offset}>{part.word}{!part.last && <>_<wbr /></>}</span>)}</>;
}

function Claims(): JSX.Element {
  return (
    <section className={styles.claims} aria-labelledby="claims-title">
      <div className={styles.container}>
        <h2 id="claims-title" className={styles.sectionTitle}>What it does, written as tests.</h2>
        <p className={styles.sectionLead}>Open any result to see the code behind it.</p>
        <div className={styles.claimList}>
          {claims.map((claim, index) => (
            <details key={claim.name} className={styles.claim} open={index === 0}>
              <summary>
                <span className={styles.passCell} aria-hidden="true" />
                <span className={styles.claimName}><TestName name={claim.name} /></span>
                <span className={styles.claimStatus}>Ready</span>
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

const installSteps = [
  {label: 'Add the pytest client', command: 'uv add revitdevtool_pytest'},
  {label: 'Run a test in Revit', command: 'uv run pytest --host revit --host-version 2025 -v'},
];

interface CopyStatus {
  readonly index: number;
  readonly ok: boolean;
}

function describeCopyStatus(status: CopyStatus | null): string {
  if (!status) return ' ';
  if (!status.ok) return 'Couldn’t copy automatically. Select the command and copy it.';
  return `${installSteps[status.index].label}: command copied.`;
}

function Start(): JSX.Element {
  const [copyStatus, setCopyStatus] = useState<CopyStatus | null>(null);

  async function copyCommand(command: string, index: number) {
    try {
      await navigator.clipboard.writeText(command);
      setCopyStatus({index, ok: true});
    } catch {
      setCopyStatus({index, ok: false});
    }
  }

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
            <li><strong>Write.</strong> Save <code>hello_script.py</code> in a folder and add the folder in the panel.</li>
            <li><strong>Run.</strong> Click the script and read the Trace panel.</li>
          </ol>
          <p className={styles.terminalLabel}>Then test it from your terminal:</p>
          <ol className={styles.terminal}>
            {installSteps.map((step, index) => (
              <li key={step.label}>
                <span className={styles.prompt} aria-hidden="true">&gt;</span>
                <code aria-label={step.label}>{step.command}</code>
                <button type="button" onClick={() => copyCommand(step.command, index)} aria-label={`Copy command: ${step.label}`}>
                  {copyStatus?.index === index && copyStatus.ok ? 'Copied' : 'Copy'}
                </button>
              </li>
            ))}
          </ol>
          <output className={styles.copyStatus}>{describeCopyStatus(copyStatus)}</output>
          <Link className={styles.primaryButton} to="/docs/getting-started/installation">Run your first script</Link>
        </div>
        <div className={styles.migration}>
          <h3>Coming from pyRevit or Add-In Manager?</h3>
          <p>
            IronPython scripts run on the pyRevit engine when it is installed, and compiled add-ins reload without a
            restart. See how the Python options compare.
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
