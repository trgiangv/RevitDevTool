import useBaseUrl from '@docusaurus/useBaseUrl';
import {useColorMode} from '@docusaurus/theme-common';
import type {JSX} from 'react';

import type {IconSource} from '../types';

interface IconProps {
  readonly source: IconSource;
  readonly className?: string;
}

export const DEVTOOLS_ICON: IconSource = {light: 'DevTools-32-Light.svg', dark: 'DevTools-32-Dark.svg'};

export function Icon({source, className}: IconProps): JSX.Element {
  const {colorMode} = useColorMode();
  const name = typeof source === 'string' ? source : source[colorMode];
  return <img className={className} src={useBaseUrl(`/icons/${name}`)} alt="" />;
}
