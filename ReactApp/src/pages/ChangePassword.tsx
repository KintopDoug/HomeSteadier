import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { z } from "zod";
import Alert from "@mui/material/Alert";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Form, FormSubmitButton, FormTextField } from "../components/Form";
import { passwordSchema } from "../validation/passwordSchema";
import { ChangePasswordViewModel } from "../viewModels/ChangePasswordViewModel";

const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, "Current password is required"),
    newPassword: passwordSchema,
    confirmPassword: z.string().min(1, "Confirm your new password"),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "Passwords do not match",
    // See ResetPassword.tsx — FormTextField only renders errors keyed by field name.
    path: ["confirmPassword"],
  });

export const ChangePassword = observer(() => {
  const viewModel = useMemo(() => {
    const vm = new ChangePasswordViewModel();
    vm.initialize();
    return vm;
  }, []);

  return (
    <div className="change-password-page">
      <Form
        className="change-password-form"
        schema={changePasswordSchema}
        values={viewModel.values}
        onSubmit={viewModel.submit}
      >
        <Typography variant="h4" component="h1" align="center">
          Change Password
        </Typography>

        <Stack spacing={2}>
          {viewModel.successMessage && (
            <Alert severity="success">{viewModel.successMessage}</Alert>
          )}

          {viewModel.errorMessage && <Alert severity="error">{viewModel.errorMessage}</Alert>}

          <FormTextField
            name="currentPassword"
            label="Current Password"
            type="password"
            fullWidth
            required
            value={viewModel.currentPassword}
            onChange={viewModel.setCurrentPassword}
          />

          <FormTextField
            name="newPassword"
            label="New Password"
            type="password"
            fullWidth
            required
            value={viewModel.newPassword}
            onChange={viewModel.setNewPassword}
          />

          <FormTextField
            name="confirmPassword"
            label="Confirm New Password"
            type="password"
            fullWidth
            required
            value={viewModel.confirmPassword}
            onChange={viewModel.setConfirmPassword}
          />

          <FormSubmitButton variant="contained" fullWidth>
            Change Password
          </FormSubmitButton>
        </Stack>
      </Form>
    </div>
  );
});
