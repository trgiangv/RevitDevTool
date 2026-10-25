import type {Selection} from '../types';

export type SelectionAction =
  | { readonly type: 'toggleStack'; readonly index: number }
  | { readonly type: 'toggleHost'; readonly index: number }
  | { readonly type: 'clear' };

export const IDLE: Selection = {kind: 'idle'};

function toggle(state: Selection, kind: 'stack' | 'host', index: number): Selection {
  const isSelected = 'index' in state && state.kind === kind && state.index === index;
  return isSelected ? IDLE : {kind, index};
}

export function selectionReducer(state: Selection, action: SelectionAction): Selection {
  switch (action.type) {
    case 'toggleStack':
      return toggle(state, 'stack', action.index);
    case 'toggleHost':
      return toggle(state, 'host', action.index);
    case 'clear':
      return state.kind === 'idle' ? state : IDLE;
  }
}

export function selectedStackIndex(selection: Selection): number {
  return selection.kind === 'stack' ? selection.index : -1;
}

export function selectedHostIndex(selection: Selection): number {
  return selection.kind === 'host' ? selection.index : -1;
}
