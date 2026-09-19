/**
 * Empty-state artwork.
 *
 * Line art rather than colour art: every stroke is `currentColor` and every
 * fill is a token, so one drawing carries both themes without a second asset.
 * The parent sets the hue — `text-[var(--nx-tint)]` for a neutral emptiness,
 * a status tone for one that went wrong — and the drawing follows.
 *
 * Each sits on a 160×120 box and scales with its container, so an
 * illustration in a full-page empty state and the same one inside a card
 * differ in size only.
 */
import type { SVGProps } from 'react'

type IllustrationProps = Omit<SVGProps<SVGSVGElement>, 'viewBox' | 'children'>

function Frame({ children, ...props }: SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox="0 0 160 120"
      fill="none"
      aria-hidden
      focusable="false"
      strokeLinecap="round"
      strokeLinejoin="round"
      {...props}
    >
      {children}
    </svg>
  )
}

/** The floor every drawing stands on — keeps the art from hovering in space. */
function Ground() {
  return (
    <line
      x1="26"
      y1="103"
      x2="134"
      y2="103"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeDasharray="4 7"
      opacity="0.34"
    />
  )
}

/** Nothing has been created yet — the default for a collection at zero. */
export function EmptyBoxIllustration(props: IllustrationProps) {
  return (
    <Frame {...props}>
      <Ground />
      <path
        d="M44 54h72v40a6 6 0 0 1-6 6H50a6 6 0 0 1-6-6V54Z"
        fill="var(--nx-fill)"
        stroke="currentColor"
        strokeWidth="2"
      />
      <path
        d="M38 40h84v14H38V40Z"
        fill="var(--nx-tint-fill)"
        stroke="currentColor"
        strokeWidth="2"
      />
      <path d="M66 68h28" stroke="currentColor" strokeWidth="2" opacity="0.5" />
      <path d="M80 40V26" stroke="currentColor" strokeWidth="2" opacity="0.35" />
      <path d="M62 32l-6-9M98 32l6-9" stroke="currentColor" strokeWidth="2" opacity="0.35" />
    </Frame>
  )
}

/** A filter or query matched nothing — the data exists, this cut of it doesn't. */
export function NoResultsIllustration(props: IllustrationProps) {
  return (
    <Frame {...props}>
      <Ground />
      <rect
        x="34"
        y="24"
        width="60"
        height="72"
        rx="8"
        fill="var(--nx-fill)"
        stroke="currentColor"
        strokeWidth="2"
      />
      <path d="M46 42h30M46 54h36M46 66h22" stroke="currentColor" strokeWidth="2" opacity="0.4" />
      <circle
        cx="104"
        cy="66"
        r="21"
        fill="var(--nx-tint-fill)"
        stroke="currentColor"
        strokeWidth="2"
      />
      <path d="M119 81l11 11" stroke="currentColor" strokeWidth="3" />
      <path d="M97 59l14 14M111 59l-14 14" stroke="currentColor" strokeWidth="2" opacity="0.6" />
    </Frame>
  )
}

/** The request failed. Paired with a retry, never with a "create" action. */
export function BrokenIllustration(props: IllustrationProps) {
  return (
    <Frame {...props}>
      <Ground />
      <path
        d="M52 30h56a6 6 0 0 1 6 6v50a6 6 0 0 1-6 6H52a6 6 0 0 1-6-6V36a6 6 0 0 1 6-6Z"
        fill="var(--nx-fill)"
        stroke="currentColor"
        strokeWidth="2"
      />
      <path d="M46 48h68" stroke="currentColor" strokeWidth="2" opacity="0.45" />
      <path d="M80 60v14" stroke="currentColor" strokeWidth="3" />
      <circle cx="80" cy="82" r="2.2" fill="currentColor" />
      <path d="M124 34l10-8M128 50h13M122 66l11 6" stroke="currentColor" strokeWidth="2" opacity="0.35" />
    </Frame>
  )
}

/** No connection — the panel is fine, the link to the data is not. */
export function OfflineIllustration(props: IllustrationProps) {
  return (
    <Frame {...props}>
      <Ground />
      <path
        d="M38 62a58 58 0 0 1 84 0"
        stroke="currentColor"
        strokeWidth="2"
        opacity="0.3"
      />
      <path d="M52 74a38 38 0 0 1 56 0" stroke="currentColor" strokeWidth="2" opacity="0.5" />
      <path d="M66 86a19 19 0 0 1 28 0" stroke="currentColor" strokeWidth="2.5" />
      <circle cx="80" cy="96" r="3.5" fill="currentColor" />
      <path
        d="M100 26l30 30M130 26l-30 30"
        stroke="currentColor"
        strokeWidth="2.5"
        opacity="0.55"
      />
    </Frame>
  )
}

/** The queue is empty because the work is finished, not because it never started. */
export function AllDoneIllustration(props: IllustrationProps) {
  return (
    <Frame {...props}>
      <Ground />
      <rect
        x="40"
        y="22"
        width="66"
        height="74"
        rx="8"
        fill="var(--nx-fill)"
        stroke="currentColor"
        strokeWidth="2"
      />
      <path d="M54 44h26M54 58h38M54 72h18" stroke="currentColor" strokeWidth="2" opacity="0.4" />
      <circle
        cx="110"
        cy="72"
        r="20"
        fill="var(--nx-tint-fill)"
        stroke="currentColor"
        strokeWidth="2"
      />
      <path d="M101 72l6.5 7 12-14" stroke="currentColor" strokeWidth="3" />
    </Frame>
  )
}

/** Access exists but is not granted here — a wall, not a void. */
export function NoAccessIllustration(props: IllustrationProps) {
  return (
    <Frame {...props}>
      <Ground />
      <path
        d="M62 56V44a18 18 0 0 1 36 0v12"
        stroke="currentColor"
        strokeWidth="2"
        opacity="0.55"
      />
      <rect
        x="48"
        y="56"
        width="64"
        height="46"
        rx="10"
        fill="var(--nx-tint-fill)"
        stroke="currentColor"
        strokeWidth="2"
      />
      <circle cx="80" cy="74" r="5" stroke="currentColor" strokeWidth="2" />
      <path d="M80 79v9" stroke="currentColor" strokeWidth="2.5" />
      <path d="M28 40h10M122 40h10M32 88h8" stroke="currentColor" strokeWidth="2" opacity="0.3" />
    </Frame>
  )
}
