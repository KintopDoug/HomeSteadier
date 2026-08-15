import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { z } from "zod";
import Alert from "@mui/material/Alert";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Link, getRouteApi } from "@tanstack/react-router";
import { Form, FormSubmitButton, FormTextField } from "../components/Form";
import { passwordSchema } from "../validation/passwordSchema";
import { ResetPasswordViewModel } from "../viewModels/ResetPasswordViewModel";

const resetPasswordSchema = z
  .object({
    token: z.string(),
    newPassword: passwordSchema,
    confirmPassword: z.string().min(1, "Confirm your new password"),
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: "Passwords do not match",
    // Required. FormTextField looks errors up as a flat errors[name], so a refine issue without
    // an explicit path lands at the form root and renders nowhere — leaving a submit button that
    // silently does nothing.
    path: ["confirmPassword"],
  });

// Via getRouteApi rather than importing Route from ./routes/reset-password: the route module
// already imports this page, so that would be a circular import.
const route = getRouteApi("/reset-password");

export const ResetPassword = observer(() => {
  const { token } = route.useSearch();

  const viewModel = useMemo(() => {
    const vm = new ResetPasswordViewModel(token);
    vm.initialize();
    return vm;
  }, [token]);

  if (!token) {
    return (
      <div className="reset-password-page">
        <Stack spacing={2} sx={{ maxWidth: 320, mx: "auto" }}>
          <Typography variant="h4" component="h1" align="center">
            Invalid Link
          </Typography>

          <Alert severity="error">
            This password reset link is missing its token. It may have been truncated by your email
            client — try copying the whole link, or request a new one.
          </Alert>

          <Typography align="center">
            <Link to="/forgot-password">Request a new link</Link>
          </Typography>
        </Stack>
      </div>
    );
  }

  return (
    <div className="reset-password-page">
      <Form
        className="reset-password-form"
        schema={resetPasswordSchema}
        values={viewModel.values}
        onSubmit={viewModel.submit}
      >
        <Typography variant="h4" component="h1" align="center">
          Choose a New Password
        </Typography>

        <Stack spacing={2}>
          {viewModel.errorMessage && <Alert severity="error">{viewModel.errorMessage}</Alert>}

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
            Reset Password
          </FormSubmitButton>
        </Stack>
      </Form>

      <Typography align="center">
        <Link to="/forgot-password">Request a new link</Link>
      </Typography>
    </div>
  );
});
