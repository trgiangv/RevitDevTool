import { createContext, useContext, useEffect, useMemo } from "react"
import { BridgeClient } from "@/lib/bridge-client"

const BridgeContext = createContext<BridgeClient | null>(null)

export function BridgeProvider({ children }: { children: React.ReactNode }) {
  const client = useMemo(() => new BridgeClient(), [])

  useEffect(() => {
    return () => client.dispose()
  }, [client])

  return <BridgeContext value={client}>{children}</BridgeContext>
}

export function useBridge(): BridgeClient {
  const ctx = useContext(BridgeContext)
  if (!ctx) throw new Error("useBridge must be used within <BridgeProvider>")
  return ctx
}
