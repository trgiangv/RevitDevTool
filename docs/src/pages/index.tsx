import React from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';

export default function Home(): React.JSX.Element {
  return (
    <Layout title="RevitDevTool" description="Standard development workflows inside live CAD/BIM hosts">
      <main className="container margin-vert--lg">
        <h1>RevitDevTool</h1>
        <p>Standard development workflows inside live Autodesk CAD/BIM hosts.</p>
        <Link className="button button--primary" to="/docs/">Read the documentation</Link>
      </main>
    </Layout>
  );
}
