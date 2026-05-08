import React, { createContext, useContext, useEffect, useMemo, useState } from 'react'

export type ColorMode = 'system' | 'light' | 'dark'
export type ThemeStyle = 'modern' | 'classic' | 'condensed' | 'minimal' | 'contrast'
export type DisplayMode = 'cards' | 'table'
export type DisplayDensity = 'condensed' | 'normal' | 'roomy'

type AppearanceContextType = {
  colorMode: ColorMode
  effectiveColorMode: 'light' | 'dark'
  themeStyle: ThemeStyle
  displayMode: DisplayMode
  displayDensity: DisplayDensity
  setColorMode: (mode: ColorMode) => void
  setThemeStyle: (style: ThemeStyle) => void
  setDisplayMode: (mode: DisplayMode) => void
  setDisplayDensity: (density: DisplayDensity) => void
}

const storageKey = 'asstrack.appearance'

const AppearanceContext = createContext<AppearanceContextType>({
  colorMode: 'system',
  effectiveColorMode: 'light',
  themeStyle: 'modern',
  displayMode: 'table',
  displayDensity: 'normal',
  setColorMode: () => undefined,
  setThemeStyle: () => undefined,
  setDisplayMode: () => undefined,
  setDisplayDensity: () => undefined,
})

function getSystemColorMode(): 'light' | 'dark' {
  if (typeof window === 'undefined') return 'light'
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function readStoredAppearance(): { colorMode: ColorMode; themeStyle: ThemeStyle; displayMode: DisplayMode; displayDensity: DisplayDensity } {
  if (typeof window === 'undefined') return { colorMode: 'system', themeStyle: 'modern', displayMode: 'table', displayDensity: 'normal' }

  try {
    const parsed = JSON.parse(window.localStorage.getItem(storageKey) ?? '{}') as Partial<{
      colorMode: ColorMode
      themeStyle: ThemeStyle
      displayMode: DisplayMode
      displayDensity: DisplayDensity
    }>
    return {
      colorMode: parsed.colorMode === 'light' || parsed.colorMode === 'dark' || parsed.colorMode === 'system'
        ? parsed.colorMode
        : 'system',
      themeStyle:
        parsed.themeStyle === 'classic' ||
        parsed.themeStyle === 'condensed' ||
        parsed.themeStyle === 'minimal' ||
        parsed.themeStyle === 'contrast' ||
        parsed.themeStyle === 'modern'
          ? parsed.themeStyle
          : 'modern',
      displayMode: parsed.displayMode === 'table' || parsed.displayMode === 'cards' ? parsed.displayMode : 'table',
      displayDensity:
        parsed.displayDensity === 'condensed' ||
        parsed.displayDensity === 'normal' ||
        parsed.displayDensity === 'roomy'
          ? parsed.displayDensity
          : 'normal',
    }
  } catch {
    return { colorMode: 'system', themeStyle: 'modern', displayMode: 'table', displayDensity: 'normal' }
  }
}

export function AppearanceProvider({ children }: { children: React.ReactNode }) {
  const stored = useMemo(readStoredAppearance, [])
  const [colorMode, setColorMode] = useState<ColorMode>(stored.colorMode)
  const [themeStyle, setThemeStyle] = useState<ThemeStyle>(stored.themeStyle)
  const [displayMode, setDisplayMode] = useState<DisplayMode>(stored.displayMode)
  const [displayDensity, setDisplayDensity] = useState<DisplayDensity>(stored.displayDensity)
  const [systemColorMode, setSystemColorMode] = useState<'light' | 'dark'>(getSystemColorMode)

  const effectiveColorMode = colorMode === 'system' ? systemColorMode : colorMode

  useEffect(() => {
    const media = window.matchMedia('(prefers-color-scheme: dark)')
    const handleChange = () => setSystemColorMode(media.matches ? 'dark' : 'light')
    handleChange()
    media.addEventListener('change', handleChange)
    return () => media.removeEventListener('change', handleChange)
  }, [])

  useEffect(() => {
    window.localStorage.setItem(storageKey, JSON.stringify({ colorMode, themeStyle, displayMode, displayDensity }))
    document.documentElement.dataset.colorMode = effectiveColorMode
    document.documentElement.dataset.themeMode = colorMode
    document.documentElement.dataset.themeStyle = themeStyle
    document.documentElement.dataset.displayDensity = displayDensity
  }, [colorMode, displayDensity, displayMode, effectiveColorMode, themeStyle])

  return (
    <AppearanceContext.Provider value={{ colorMode, effectiveColorMode, themeStyle, displayMode, displayDensity, setColorMode, setThemeStyle, setDisplayMode, setDisplayDensity }}>
      {children}
    </AppearanceContext.Provider>
  )
}

export function useAppearance(): AppearanceContextType {
  return useContext(AppearanceContext)
}
