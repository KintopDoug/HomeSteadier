import { useFormContext } from "react-hook-form";
import Button from "@mui/material/Button";
import type { ButtonProps } from "@mui/material/Button";

export const FormSubmitButton = ({ children, disabled, ...buttonProps }: ButtonProps) => {
  const {
    formState: { isSubmitting },
  } = useFormContext();

  return (
    <Button type="submit" disabled={disabled || isSubmitting} {...buttonProps}>
      {children}
    </Button>
  );
};
