import { FormProvider, useForm } from "react-hook-form";
import type { FieldValues, SubmitHandler } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import type { ZodType } from "zod";
import type { ReactNode } from "react";

interface FormProps<TFieldValues extends FieldValues> {
  schema: ZodType<TFieldValues, TFieldValues>;
  values: TFieldValues;
  onSubmit: SubmitHandler<TFieldValues>;
  children: ReactNode;
  id?: string;
  className?: string;
}

/**
 * Field values are owned by the caller (typically a MobX viewModel), not by
 * react-hook-form's internal state. `values` keeps RHF's copy in sync so the
 * zod resolver can validate against it; RHF here is purely the validation
 * and submit-orchestration layer.
 */
export const Form = <TFieldValues extends FieldValues>({
  schema,
  values,
  onSubmit,
  children,
  id,
  className,
}: FormProps<TFieldValues>) => {
  const methods = useForm<TFieldValues>({
    resolver: zodResolver(schema),
    values,
    mode: "onBlur",
  });

  return (
    <FormProvider {...methods}>
      <form id={id} className={className} onSubmit={methods.handleSubmit(onSubmit)} noValidate>
        {children}
      </form>
    </FormProvider>
  );
};
