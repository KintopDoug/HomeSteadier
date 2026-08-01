import { useFormContext } from "react-hook-form";
import TextField from "@mui/material/TextField";
import type { TextFieldProps } from "@mui/material/TextField";
import type { ChangeEvent, FocusEvent } from "react";

interface FormTextFieldProps extends Omit<TextFieldProps, "name" | "error" | "defaultValue" | "onChange"> {
  name: string;
  value: string;
  onChange: (value: string) => void;
}

/**
 * Controlled by the caller's `value`/`onChange` (a MobX viewModel field),
 * not react-hook-form registration — this component only reads validation
 * state from RHF's form context and triggers validation on blur/change.
 */
export const FormTextField = ({ name, value, onChange, onBlur, helperText, ...textFieldProps }: FormTextFieldProps) => {
  const {
    formState: { errors },
    trigger,
    setValue,
  } = useFormContext();
  const error = errors[name];

  const handleChange = (event: ChangeEvent<HTMLInputElement>) => {
    const newValue = event.target.value;
    onChange(newValue);
    // Sync react-hook-form's copy immediately rather than waiting on the
    // Form's `values` prop to resync from the viewModel on the next render,
    // so revalidation checks the value the user just typed, not a stale one.
    setValue(name, newValue, { shouldValidate: !!error });
  };

  const handleBlur = (event: FocusEvent<HTMLInputElement>) => {
    onBlur?.(event);
    void trigger(name);
  };

  return (
    <TextField
      {...textFieldProps}
      name={name}
      value={value}
      onChange={handleChange}
      onBlur={handleBlur}
      error={!!error}
      helperText={(error?.message as string | undefined) ?? helperText}
    />
  );
};
