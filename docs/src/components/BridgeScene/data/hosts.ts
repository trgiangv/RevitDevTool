import type {HostDefinition} from '../types';

export const HOSTS: readonly HostDefinition[] = [
  {
    name: 'Revit',
    icon: 'revit-icon.svg',
    y: 3.6,
    bullets: [
      'BIM software for designing, documenting and coordinating buildings',
      'Parametric models that keep plans, sections and schedules consistent',
      'Multidiscipline coordination for architecture, structure and MEP',
    ],
    link: {label: 'Autodesk · Revit', href: 'https://www.autodesk.com/products/revit/overview'},
  },
  {
    name: 'AutoCAD',
    icon: 'autocad-icon.svg',
    y: 1.2,
    bullets: [
      '2D and 3D drafting and documentation software',
      'Precise drawings, blocks, layers and annotation',
      'Industry-specific toolsets available for sectors such as architecture, electrical and plant design',
    ],
    link: {label: 'Autodesk · AutoCAD', href: 'https://www.autodesk.com/products/autocad/overview'},
  },
  {
    name: 'Civil 3D',
    icon: 'civil3d-icon.svg',
    y: -1.2,
    bullets: [
      'Civil engineering design and documentation software, built on AutoCAD',
      'Surfaces, alignments, corridors and grading for roads and sites',
      'Pipe networks and survey data in a dynamic model',
    ],
    link: {label: 'Autodesk · Civil 3D', href: 'https://www.autodesk.com/products/civil-3d/overview'},
  },
  {
    name: 'Plant 3D',
    icon: 'plant3d-icon.svg',
    y: -3.6,
    bullets: [
      'AutoCAD toolset for designing process plants',
      'P&ID drafting linked to a 3D plant model',
      'Piping, equipment and steel, with isometric drawings generated from the model',
    ],
    link: {label: 'Autodesk · Plant 3D', href: 'https://www.autodesk.com/products/autocad/included-toolsets/autocad-plant-3d'},
  },
];

export const MORE_HOSTS_LABEL = '+ more';
