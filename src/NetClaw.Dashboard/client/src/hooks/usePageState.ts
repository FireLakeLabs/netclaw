import { useState, useCallback, useEffect } from "react";

const store = new Map<string, unknown>();

export function usePageState<T>(key: string, initial: T): [T, (value: T | ((prev: T) => T)) => void] {
  const [state, setStateRaw] = useState<T>(() =>
    store.has(key) ? (store.get(key) as T) : initial,
  );

  useEffect(() => {
    setStateRaw(store.has(key) ? (store.get(key) as T) : initial);
  }, [key, initial]);

  const setState = useCallback(
    (value: T | ((prev: T) => T)) => {
      setStateRaw((prev) => {
        const next = typeof value === "function" ? (value as (prev: T) => T)(prev) : value;
        store.set(key, next);
        return next;
      });
    },
    [key],
  );

  return [state, setState];
}
