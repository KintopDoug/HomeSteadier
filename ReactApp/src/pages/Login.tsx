import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { z } from "zod";
import Alert from "@mui/material/Alert";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Link } from "@tanstack/react-router";
import { Form, FormSubmitButton, FormTextField } from "../components/Form";
import { LoginViewModel } from "../viewModels/LoginViewModel";

const loginSchema = z.object({
  email: z.email("Enter a valid email address").min(1, "Email is required"),
  password: z.string().min(1, "Password is required"),
});

export const Login = observer(() => {
  const viewModel = useMemo(() => {
    const vm = new LoginViewModel();
    vm.initialize();
    return vm;
  }, []);

  return (
    <div className="login-page">
      <Form
        className="login-form"
        schema={loginSchema}
        values={viewModel.values}
        onSubmit={viewModel.submit}
      >
        <Typography variant="h4" component="h1" align="center">
          Sign In
        </Typography>

        <Stack spacing={2}>
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

          <FormTextField
            name="password"
            label="Password"
            type="password"
            fullWidth
            required
            value={viewModel.password}
            onChange={viewModel.setPassword}
          />

          <FormSubmitButton variant="contained" fullWidth>
            Sign In
          </FormSubmitButton>

          <Typography variant="body2" align="center">
            <Link to="/forgot-password">Forgot password?</Link>
          </Typography>
        </Stack>
      </Form>

      <Typography align="center">
        Don't have an account? <Link to="/register">Sign up</Link>
      </Typography>
    </div>
  );
});
