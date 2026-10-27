import type { ReactNode } from 'react'
import type { HomeSectionId } from '../homeSections.ts'

type HomeSectionProps = {
  id: HomeSectionId
  title: string
  className?: string
  children: ReactNode
}

/** Sekcja strony głównej z nagłówkiem. */
export function HomeSection({ id, title, className, children }: HomeSectionProps) {
  const titleId = `${id}-title`

  return (
    <section
      id={id}
      className={className ? `section ${className}` : 'section'}
      aria-labelledby={titleId}
    >
      <h2 id={titleId}>{title}</h2>
      {children}
    </section>
  )
}
