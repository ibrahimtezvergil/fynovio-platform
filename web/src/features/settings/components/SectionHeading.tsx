/** 17px section title over a 12.5px explainer — the artboard's card heading. */
export function SectionHeading({
  title,
  description,
}: {
  title: string
  description: string
}) {
  return (
    <div className="flex min-w-0 flex-1 flex-col gap-0.5">
      <h2 className="font-heading text-[17px] leading-tight font-[620] tracking-[-0.024em]">
        {title}
      </h2>
      <p className="text-muted-foreground text-[12.5px]">{description}</p>
    </div>
  )
}
