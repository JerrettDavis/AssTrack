import { useAppearance, type DisplayDensity, type DisplayMode } from '../context/AppearanceContext'

const modes: Array<{ value: DisplayMode; label: string }> = [
  { value: 'cards', label: 'Cards' },
  { value: 'table', label: 'Table' },
]

const densities: Array<{ value: DisplayDensity; label: string }> = [
  { value: 'condensed', label: 'Condensed' },
  { value: 'normal', label: 'Normal' },
  { value: 'roomy', label: 'Roomy' },
]

type DisplayControlsProps = {
  mode?: DisplayMode
  onModeChange?: (mode: DisplayMode) => void
}

export default function DisplayControls({ mode, onModeChange }: DisplayControlsProps) {
  const { displayMode, displayDensity, setDisplayMode, setDisplayDensity } = useAppearance()
  const effectiveMode = mode ?? displayMode
  const setEffectiveMode = onModeChange ?? setDisplayMode

  return (
    <div className="display-controls" aria-label="Display preferences">
      <div className="segmented-control compact-segmented" aria-label="View mode">
        {modes.map((mode) => (
          <button className={effectiveMode === mode.value ? 'active' : ''} key={mode.value} onClick={() => setEffectiveMode(mode.value)} type="button">
            {mode.label}
          </button>
        ))}
      </div>
      <div className="segmented-control compact-segmented" aria-label="Spacing">
        {densities.map((density) => (
          <button className={displayDensity === density.value ? 'active' : ''} key={density.value} onClick={() => setDisplayDensity(density.value)} type="button">
            {density.label}
          </button>
        ))}
      </div>
    </div>
  )
}
