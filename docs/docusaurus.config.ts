import type { Config } from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';
import { themes as prismThemes } from 'prism-react-renderer';

const config: Config = {
  title: 'RevitDevTool',
  tagline: 'Standard development workflows inside live CAD/BIM hosts',
  favicon: 'icons/DevTools-32-Light.svg',
  url: 'https://trgiangv.github.io',
  baseUrl: '/RevitDevTool/',
  organizationName: 'trgiangv',
  projectName: 'RevitDevTool',
  deploymentBranch: 'gh-pages',
  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',
  i18n: { defaultLocale: 'en', locales: ['en'] },
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
  themes: ['@docusaurus/theme-mermaid'],
  themeConfig: {
    navbar: {
      title: 'RevitDevTool',
      logo: { alt: 'RevitDevTool', src: 'icons/DevTools-32-Light.svg' },
      items: [
        { type: 'docSidebar', sidebarId: 'docs', position: 'left', label: 'Docs' },
        { href: 'https://github.com/trgiangv/RevitDevTool', label: 'GitHub', position: 'right' },
        { href: 'https://github.com/trgiangv/RevitDevTool/releases', label: 'Releases', position: 'right' },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        { title: 'Documentation', items: [{ label: 'Getting Started', to: '/docs/' }] },
        { title: 'Community', items: [{ label: 'Issues', href: 'https://github.com/trgiangv/RevitDevTool/issues' }, { label: 'Discussions', href: 'https://github.com/trgiangv/RevitDevTool/discussions' }] },
      ],
      copyright: `Copyright © ${new Date().getFullYear()} RevitDevTool contributors.`,
    },
    prism: {
      additionalLanguages: ['csharp', 'fsharp', 'python', 'powershell', 'json'],
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
