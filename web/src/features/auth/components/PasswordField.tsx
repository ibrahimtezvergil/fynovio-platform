import { Controller, type Control, type FieldValues, type Path } from 'react-hook-form'
import { Field } from '@/components/common/Field'
import { PasswordInput } from '@/components/common/inputs/PasswordInput'

interface PasswordFieldProps<T extends FieldValues> {
  control: Control<T>
  name: Path<T>
  label: string
  error?: string
  hint?: string
  autoComplete: 'current-password' | 'new-password'
}

/** Label · masked input · hint/error, wired to react-hook-form. The one place a password field is built. */
export function PasswordField<T extends FieldValues>({ control, name, label, error, hint, autoComplete }: PasswordFieldProps<T>) {
  return (
    <Field label={label} hint={hint} error={error}>
      {(props) => (
        <Controller
          control={control}
          name={name}
          render={({ field }) => (
            <PasswordInput {...props} value={field.value ?? ''} onValueChange={field.onChange} autoComplete={autoComplete} />
          )}
        />
      )}
    </Field>
  )
}
