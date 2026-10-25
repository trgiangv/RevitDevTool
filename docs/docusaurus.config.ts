import type { Config } from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';
import { themes as prismThemes } from 'prism-react-renderer';

const config: Config = {
  title: 'RevitDevTool',
  tagline: 'Standard development workflows inside live CAD/BIM hosts',
  headTags: [
    { tagName: 'link', attributes: { rel: 'icon', type: 'image/svg+xml', href: '/RevitDevTool/icons/DevTools-32-Light.svg', media: '(prefers-color-scheme: light)' } },
    { tagName: 'link', attributes: { rel: 'icon', type: 'image/svg+xml', href: '/RevitDevTool/icons/DevTools-32-Dark.svg', media: '(prefers-color-scheme: dark)' } },
    {
      tagName: 'script',
      attributes: {},
      innerHTML: `window.tlumaConfig = {
  source: 'trgiangv/revitdevtool',
  theme: 'auto',
  brandColor: 'blue',
  button: 'bottom-right',
  welcomePulse: true,
  edgePadding: '1rem',
  autoOpen: false,
  desktopFullscreenByDefault: false,
};`,
    },
    { tagName: 'script', attributes: { src: 'https://tluma.ai/widget.js', async: 'true' } },
  ],
  url: 'https://trgiangv.github.io',
  baseUrl: '/RevitDevTool/',
  organizationName: 'trgiangv',
  projectName: 'RevitDevTool',
  deploymentBranch: 'gh-pages',
  trailingSlash: true,
  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',
  i18n: { defaultLocale: 'en', locales: ['en'] },
  stylesheets: [
    'https://fonts.googleapis.com/css2?family=Hanken+Grotesk:ital,wght@0,300..800;1,300..800&family=Martian+Mono:wdth,wght@75..112.5,300..700&display=swap',
  ],
  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          routeBasePath: 'docs',
          showLastUpdateTime: true,
          editUrl: 'https://github.com/trgiangv/RevitDevTool/edit/develop/docs/',
        },
        blog: false,
        theme: { customCss: './src/css/custom.css' },
      } satisfies Preset.Options,
    ],
  ],
  markdown: {
    mermaid: true,
    hooks: { onBrokenMarkdownLinks: 'throw' },
  },
  themes: [
    '@docusaurus/theme-mermaid',
    [
      '@easyops-cn/docusaurus-search-local',
      {
        hashed: true,
        language: ['en'],
        indexBlog: false,
        indexPages: false,
        docsRouteBasePath: '/docs',
        highlightSearchTermsOnTargetPage: true,
        explicitSearchResultPath: true,
        searchResultLimits: 8,
      },
    ],
  ],
  themeConfig: {
    colorMode: { respectPrefersColorScheme: true },
    mermaid: {
      theme: { light: 'base', dark: 'base' },
      options: {
        fontFamily: "'Hanken Grotesk', system-ui, sans-serif",
        themeVariables: { fontSize: '14px' },
        flowchart: { curve: 'basis', nodeSpacing: 42, rankSpacing: 52, padding: 14, htmlLabels: true },
        sequence: { actorMargin: 60, messageMargin: 34, boxMargin: 8, mirrorActors: false, useMaxWidth: true },
      },
    },
    navbar: {
      title: 'RevitDevTool',
      logo: { alt: 'RevitDevTool', src: 'icons/DevTools-32-Light.svg', srcDark: 'icons/DevTools-32-Dark.svg' },
      items: [
        { type: 'docSidebar', sidebarId: 'docs', position: 'left', label: 'Docs' },
        { href: 'https://github.com/trgiangv/RevitDevTool/issues', label: 'Issues', position: 'left' },
        { href: 'https://github.com/trgiangv/RevitDevTool/discussions', label: 'Discussions', position: 'left' },
        { href: 'https://github.com/trgiangv/RevitDevTool/releases', label: 'Releases', position: 'right' },
        { href: 'https://www.nuget.org/packages/RevitDevTool.TestAdapter', label: 'NuGet', position: 'right' },
        { href: 'https://pypi.org/project/revitdevtool_pytest/', label: 'PyPI', position: 'right' },
        { href: 'https://github.com/trgiangv/RevitDevTool', label: 'GitHub', position: 'right' },
      ],
    },
    footer: {
      style: 'light',
      logo: { alt: 'RevitDevTool', src: 'icons/DevTools-32-Light.svg', srcDark: 'icons/DevTools-32-Dark.svg', width: 28, height: 28 },
      links: [
        {
          title: 'Get started',
          items: [
            { label: 'Overview', to: '/docs/' },
            { label: 'Installation', to: '/docs/getting-started/installation' },
            { label: 'Troubleshooting', to: '/docs/getting-started/Troubleshooting' },
          ],
        },
        {
          title: 'Build',
          items: [
            { label: 'Run code', to: '/docs/execution/Execution-Overview' },
            { label: 'Test in the host', to: '/docs/testing/Testing-Overview' },
            { label: 'Connect AI (MCP)', to: '/docs/mcp/MCP-Protocol' },
            { label: 'Logging', to: '/docs/logging/Logging-Overview' },
          ],
        },
        {
          title: 'Project',
          items: [
            { label: 'GitHub', href: 'https://github.com/trgiangv/RevitDevTool' },
            { label: 'Releases', href: 'https://github.com/trgiangv/RevitDevTool/releases' },
            { label: 'Issues', href: 'https://github.com/trgiangv/RevitDevTool/issues' },
            { label: 'Discussions', href: 'https://github.com/trgiangv/RevitDevTool/discussions' },
          ],
        },
      ],
      copyright: `MIT License · © ${new Date().getFullYear()} trgiangv and RevitDevTool contributors`,
    },
    prism: {
      additionalLanguages: ['csharp', 'fsharp', 'python', 'powershell', 'json'],
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
