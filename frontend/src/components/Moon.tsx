import { useId } from 'react'

type MoonProps = {
  className?: string
}

/** Złoty sierp księżyca z gwiazdami, motyw przewodni marki. */
export function Moon({ className }: MoonProps) {
  const gradientId = useId()

  return (
    <svg
      className={className}
      viewBox="0 0 120 120"
      aria-hidden="true"
      focusable="false"
    >
      <defs>
        <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="1">
          <stop offset="0%" stopColor="#f3dfa2" />
          <stop offset="55%" stopColor="#d4af61" />
          <stop offset="100%" stopColor="#a8823c" />
        </linearGradient>
      </defs>
      <path
        d="M78 14a48 48 0 1 0 28 76A40 40 0 1 1 78 14z"
        fill={`url(#${gradientId})`}
      />
      <path
        d="M96 22l2.2 5.8L104 30l-5.8 2.2L96 38l-2.2-5.8L88 30l5.8-2.2z"
        fill="#f3dfa2"
      />
      <path
        d="M108 52l1.3 3.2 3.2 1.3-3.2 1.3-1.3 3.2-1.3-3.2-3.2-1.3 3.2-1.3z"
        fill="#f3dfa2"
      />
    </svg>
  )
}
