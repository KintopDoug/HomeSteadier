import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { z } from "zod";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Link, getRouteApi } from "@tanstack/react-router";
import { Form, FormSubmitButton, FormTextField } from "../components/Form";
import { passwordSchema } from "../validation/passwordSchema";
import { SignUpViewModel } from "../viewModels/SignUpViewModel";

const signUpSchema = z.object({
  email: z.email("Enter a valid email address").min(1, "Email is required"),
  password: passwordSchema,
  firstName: z.string().min(1, "First name is required"),
  lastName: z.string().min(1, "Last name is required"),
  inviteToken: z.string().nullish(),
});

// Via getRouteApi rather than importing Route from ./routes/register: the route module already
// imports this page, so that would be a circular import.
const route = getRouteApi("/register");

export const SignUp = observer(() => {
  const { inviteToken } = route.useSearch();

  const viewModel = useMemo(() => {
    const vm = new SignUpViewModel(inviteToken);
    vm.initialize();
    return vm;
  }, [inviteToken]);

  if (viewModel.isLoadingInvitation) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", pt: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  return (
    <div className="signup-page">
      <Form
        className="signup-form"
        schema={signUpSchema}
        values={viewModel.values}
        onSubmit={viewModel.submit}
      >
        <Typography variant="h4" component="h1" align="center">
          Create Your Account
        </Typography>

        <Stack spacing={2}>
          {viewModel.invitation && (
            <Alert severity="info">
              You've been invited to join <strong>{viewModel.invitation.farmName}</strong> as{" "}
              <strong>{viewModel.invitation.roleName}</strong>.
            </Alert>
          )}

          {viewModel.invitationError && <Alert severity="error">{viewModel.invitationError}</Alert>}

          {viewModel.errorMessage && (
            <Alert severity="error">{viewModel.errorMessage}</Alert>
          )}

          <FormTextField
            name="email"
            label="Email"
            type="email"
            fullWidth
            required
            disabled={viewModel.isEmailLocked}
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

          <FormTextField
            name="firstName"
            label="First Name"
            fullWidth
            required
            value={viewModel.firstName}
            onChange={viewModel.setFirstName}
          />

          <FormTextField
            name="lastName"
            label="Last Name"
            fullWidth
            required
            value={viewModel.lastName}
            onChange={viewModel.setLastName}
          />
          <FormSubmitButton variant="contained" fullWidth>
            Sign Up
          </FormSubmitButton>
        </Stack>
      </Form>

      <Typography align="center">
        Already have an account? <Link to="/login">Sign in</Link>
      </Typography>
    </div>
  );
});
