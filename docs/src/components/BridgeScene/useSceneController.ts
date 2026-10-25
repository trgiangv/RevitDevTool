import {useCallback, useEffect, useRef, useState, type Dispatch, type RefObject} from 'react';

import {useReducedMotion} from './hooks';
import type {OverlayRegistry} from './scene/overlay';
import {SceneController, type SceneCallbacks} from './scene/SceneController';
import type {SelectionAction} from './state/selection';
import type {Selection} from './types';

interface SceneControllerInput {
  readonly stageRef: RefObject<HTMLDivElement | null>;
  readonly canvasRef: RefObject<HTMLCanvasElement | null>;
  readonly overlay: OverlayRegistry;
  readonly dark: boolean;
  readonly selection: Selection;
  readonly hoveredStack: number;
  readonly dispatch: Dispatch<SelectionAction>;
}

interface SceneControllerState {
  readonly beat: number;
  readonly hoveredHost: number;
  readonly failed: boolean;
  readonly reset: () => void;
}

interface CallbackBindings {
  readonly dispatch: Dispatch<SelectionAction>;
  readonly reset: () => void;
  readonly setBeat: (beat: number) => void;
  readonly setHoveredHost: (index: number) => void;
}

function hostTapAction(index: number): SelectionAction {
  return index >= 0 ? {type: 'toggleHost', index} : {type: 'clear'};
}

function sceneCallbacks({dispatch, reset, setBeat, setHoveredHost}: CallbackBindings): SceneCallbacks {
  return {
    onBeat: setBeat,
    onTapHost: (index) => dispatch(hostTapAction(index)),
    onHoverHost: setHoveredHost,
    onReset: reset,
    onHostKey: (index) => dispatch({type: 'toggleHost', index}),
  };
}

export function useSceneController(input: SceneControllerInput): SceneControllerState {
  const {stageRef, canvasRef, overlay, dark, selection, hoveredStack, dispatch} = input;
  const reducedMotion = useReducedMotion();
  const controllerRef = useRef<SceneController | null>(null);
  const darkRef = useRef(dark);
  darkRef.current = dark;
  const selectionRef = useRef(selection);
  selectionRef.current = selection;
  const [beat, setBeat] = useState(-1);
  const [hoveredHost, setHoveredHost] = useState(-1);
  const [failed, setFailed] = useState(false);

  const reset = useCallback(() => {
    dispatch({type: 'clear'});
    controllerRef.current?.resetView();
  }, [dispatch]);

  useEffect(() => {
    const canvas = canvasRef.current;
    const stage = stageRef.current;
    if (!canvas || !stage) return undefined;
    const controller = SceneController.create({
      canvas,
      stage,
      overlay,
      reducedMotion,
      dark: darkRef.current,
      callbacks: sceneCallbacks({dispatch, reset, setBeat, setHoveredHost}),
    });
    if (!controller) {
      setFailed(true);
      return undefined;
    }
    controllerRef.current = controller;
    controller.setSelection(selectionRef.current);
    return () => {
      controller.dispose();
      controllerRef.current = null;
    };
  }, [canvasRef, stageRef, overlay, reducedMotion, reset, dispatch]);

  useEffect(() => controllerRef.current?.setDark(dark), [dark]);
  useEffect(() => controllerRef.current?.setHoveredStack(hoveredStack), [hoveredStack]);
  useEffect(() => controllerRef.current?.setSelection(selection), [selection]);

  return {beat, hoveredHost, failed, reset};
}
