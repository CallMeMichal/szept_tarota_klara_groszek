type PlaceholderNoteProps = {
  show?: boolean
}

/** Znacznik sekcji z treścią tymczasową, do podmiany na tekst od Klary. */
export function PlaceholderNote({ show }: PlaceholderNoteProps) {
  if (!show) return null

  return (
    <p className="placeholder-note">Treść tymczasowa · do podmiany na tekst od Klary</p>
  )
}
