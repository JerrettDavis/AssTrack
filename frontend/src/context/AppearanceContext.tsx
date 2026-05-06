import React, { createContext, useContext, useEffect, useMemo, useState } from 'react'

export type ColorMode = 'system' | 'light' | 'dark'
export type ThemeStyle = 'modern' | 'classic' | 'condensed' | 'minimal' | 'contrast'

type AppearanceContextType = {
  colorMode: ColorMode
  effectiveColorMode: 'light' | 'dark'
  themeStyle: ThemeStyle
  setColorMode: (mode: ColorMode) => void
  setThemeStyle: (style: ThemeStyle) => void
}

const storageKey = 'asstrack.appearance'

const AppearanceContext = createContext<AppearanceContextType>({
  colorMode: 'system',
  effectiveColorMode: 'light',
  themeStyle: 'modern',
  setColorMode: () => undefined,
  setThemeStyle: () => undefined,
})

function getSystemColorMode(): 'light' | 'dark' {
  if (typeof window === 'undefined') return 'light'
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function readStoredAppearance(): { colorMode: ColorMode; themeStyle: ThemeStyle } {
  if (typeof window === 'undefined') return { colorMode: 'system', themeStyle: 'modern' }

  try {
    const parsed = JSON.parse(window.localStorage.getItem(storageKey) ?? '{}') as Partial<{
      colorMode: ColorMode
      themeStyle: ThemeStyle
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
    }
  } catch {
    return { colorMode: 'system', themeStyle: 'modern' }
  }
}

export function AppearanceProvider({ children }: { children: React.ReactNode }) {
  const stored = useMemo(readStoredAppearance, [])
  const [colorMode, setColorMode] = useState<ColorMode>(stored.colorMode)
  const [themeStyle, setThemeStyle] = useState<ThemeStyle>(stored.themeStyle)
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
    window.localStorage.setItem(storageKey, JSON.stringify({ colorMode, themeStyle }))
    document.documentElement.dataset.colorMode = effectiveColorMode
    document.documentElement.dataset.themeMode = colorMode
    document.documentElement.dataset.themeStyle = themeStyle
  }, [colorMode, effectiveColorMode, themeStyle])

  return (
    <AppearanceContext.Provider value={{ colorMode, effectiveColorMode, themeStyle, setColorMode, setThemeStyle }}>
      {children}
    </AppearanceContext.Provider>
  )
}

export function useAppearance(): AppearanceContextType {
  return useContext(AppearanceContext)
}
