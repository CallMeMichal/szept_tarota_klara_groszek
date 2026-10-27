import type { ReactNode } from 'react'
import type { HomeSectionId } from '../homeSections.ts'
import { PlaceholderNote } from './PlaceholderNote.tsx'

type HomeSectionProps = {
  id: HomeSectionId
  title: string
  placeholder?: boolean
  className?: string
  children: ReactNode
}

/** Sekcja strony głównej z nagłówkiem i znacznikiem treści tymczasowej. */
export function HomeSection({ id, title, placeholder, className, children }: HomeSectionProps) {
  const titleId = `${id}-title`

  return (
    <section
      id={id}
      className={className ? `section ${className}` : 'section'}
      aria-labelledby={titleId}
    >
      <h2 id={titleId}>{title}</h2>
      {children}
      <PlaceholderNote show={placeholder} />
    </section>
  )
}
