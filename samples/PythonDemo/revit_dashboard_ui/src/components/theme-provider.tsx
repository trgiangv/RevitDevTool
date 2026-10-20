import { createContext, useContext, useEffect, useMemo, useState } from "react"

type Theme = "dark" | "light" | "system"

interface ThemeProviderProps {
  children: React.ReactNode
  defaultTheme?: Theme
  storageKey?: string
}

interface ThemeProviderState {
  theme: Theme
  setTheme: (theme: Theme) => void
  resolvedTheme: "dark" | "light"
}

const initialState: ThemeProviderState = {
  theme: "system",
  setTheme: () => null,
  resolvedTheme: "light",
}

const ThemeProviderContext = createContext<ThemeProviderState>(initialState)

function getResolvedTheme(theme: Theme): "dark" | "light" {
  if (theme === "system") {
    return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light"
  }
  return theme
}

export function ThemeProvider({
  children,
  defaultTheme = "system",
  storageKey = "bim-dashboard-theme",
  ...props
}: Readonly<ThemeProviderProps>) {
  const [theme, setTheme] = useState<Theme>(
    () => (localStorage.getItem(storageKey) as Theme) || defaultTheme,
  )

  const resolvedTheme = useMemo(() => getResolvedTheme(theme), [theme])

  if (typeof document !== "undefined") {
    document.body.classList.add("semi-light-scrollbar")
    if (resolvedTheme === "dark") document.body.setAttribute("theme-mode", "dark")
    else document.body.removeAttribute("theme-mode")
  }

  // Sync both Tailwind dark class AND Semi Design theme-mode attribute
  useEffect(() => {
    const root = document.documentElement
    const body = document.body

    // Tailwind dark class on <html>
    root.classList.remove("light", "dark")
    root.classList.add(resolvedTheme)

    // Semi scrollbar tokens. The class styles this node and every scrolling descendant.
    body.classList.add("semi-light-scrollbar")

    // Semi Design dark mode on <body>
    if (resolvedTheme === "dark") {
      body.setAttribute("theme-mode", "dark")
    } else {
      body.removeAttribute("theme-mode")
    }
  }, [resolvedTheme])

  // Listen for system theme changes
  useEffect(() => {
    if (theme !== "system") return

    const mediaQuery = window.matchMedia("(prefers-color-scheme: dark)")
    const handleChange = () => {
      setTheme((prev) => prev)
    }

    mediaQuery.addEventListener("change", handleChange)
    return () => mediaQuery.removeEventListener("change", handleChange)
  }, [theme])

  const value = useMemo(
    () => ({
      theme,
      setTheme: (newTheme: Theme) => {
        localStorage.setItem(storageKey, newTheme)
        setTheme(newTheme)
      },
      resolvedTheme,
    }),
    [theme, resolvedTheme, storageKey],
  )

  return (
    <ThemeProviderContext.Provider {...props} value={value}>
      {children}
    </ThemeProviderContext.Provider>
  )
}

export function useTheme() {
  const context = useContext(ThemeProviderContext)
  if (context === undefined) throw new Error("useTheme must be used within a ThemeProvider")
  return context
}
