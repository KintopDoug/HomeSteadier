import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { z } from "zod";
import Alert from "@mui/material/Alert";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Link } from "@tanstack/react-router";
import { Form, FormSubmitButton, FormTextField } from "../components/Form";
import { ForgotPasswordViewModel } from "../viewModels/ForgotPasswordViewModel";

const forgotPasswordSchema = z.object({
  email: z.email("Enter a valid email address").min(1, "Email is required"),
});

export const ForgotPassword = observer(() => {
  const viewModel = useMemo(() => {
    const vm = new ForgotPasswordViewModel();
    vm.initialize();
    return vm;
  }, []);

  return (
    <div className="forgot-password-page">
      {viewModel.successMessage ? (
        <Stack spacing={2} sx={{ maxWidth: 320, mx: "auto" }}>
          <Typography variant="h4" component="h1" align="center">
            Check Your Email
          </Typography>

          <Alert severity="success">{viewModel.successMessage}</Alert>

          <Typography align="center">
            <Link to="/login">Back to sign in</Link>
          </Typography>
        </Stack>
      ) : (
        <>
          <Form
            className="forgot-password-form"
            schema={forgotPasswordSchema}
            values={viewModel.values}
            onSubmit={viewModel.submit}
          >
            <Typography variant="h4" component="h1" align="center">
              Reset Your Password
            </Typography>

            <Stack spacing={2}>
              <Typography variant="body2" color="text.secondary" align="center">
                Enter your email address and we'll send you a link to choose a new password.
              </Typography>

              {viewModel.errorMessage && (
                <Alert severity="error">{viewModel.errorMessage}</Alert>
              )}

              <FormTextField
                name="email"
                label="Email"
                type="email"
                fullWidth
                required
                value={viewModel.email}
                onChange={viewModel.setEmail}
              />

              <FormSubmitButton variant="contained" fullWidth>
                Send Reset Link
              </FormSubmitButton>
            </Stack>
          </Form>

          <Typography align="center">
            Remembered it? <Link to="/login">Sign in</Link>
          </Typography>
        </>
      )}
    </div>
  );
});
