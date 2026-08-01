import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { z } from "zod";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Form, FormSubmitButton, FormTextField } from "../components/Form";
import { SignUpViewModel } from "../viewModels/SignUpViewModel";

const signUpSchema = z.object({
  email: z.email("Enter a valid email address").min(1, "Email is required"),
  password: z.string().min(8, "Password must be at least 8 characters"),
  firstName: z.string().min(1, "First name is required"),
  lastName: z.string().min(1, "Last name is required"),
});

export const SignUp = observer(() => {
  const viewModel = useMemo(() => {
    const vm = new SignUpViewModel();
    vm.initialize();
    return vm;
  }, []);

  return (
    <div className="signup-page">
      <Form
        className="signup-form"
        schema={signUpSchema}
        values={viewModel.values}
        onSubmit={viewModel.submit}
      >
        <Typography variant="h4" component="h1" align="center">
          Sign Up
        </Typography>

        <Stack spacing={2}>
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
    </div>
  );
});
