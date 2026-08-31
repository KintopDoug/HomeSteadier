import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { z } from "zod";
import Alert from "@mui/material/Alert";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Form, FormSubmitButton, FormTextField } from "../components/Form";
import { InviteMemberViewModel } from "../viewModels/InviteMemberViewModel";
import { session } from "../stores/SessionStore";

const inviteMemberSchema = z.object({
  email: z.email("Enter a valid email address").min(1, "Email is required"),
  farmRoleTypeId: z.string().min(1, "Select a role"),
});

export const InviteMember = observer(() => {
  const viewModel = useMemo(() => {
    const vm = new InviteMemberViewModel();
    vm.initialize();
    return vm;
  }, []);

  if (session.activeFarm?.roleName !== "Admin") {
    return (
      <div className="invite-member-page">
        <Stack spacing={2} sx={{ maxWidth: 400, mx: "auto" }}>
          <Typography variant="h4" component="h1" align="center">
            Invite to Farm
          </Typography>
          <Alert severity="error">Only a farm admin can invite new members.</Alert>
        </Stack>
      </div>
    );
  }

  return (
    <div className="invite-member-page">
      <Form schema={inviteMemberSchema} values={viewModel.values} onSubmit={viewModel.submit}>
        <Typography variant="h4" component="h1" align="center" sx={{ mb: 1 }}>
          Invite to {session.activeFarm.name}
        </Typography>

        <Stack spacing={2} sx={{ maxWidth: 400, mx: "auto" }}>
          {viewModel.successMessage && <Alert severity="success">{viewModel.successMessage}</Alert>}
          {viewModel.errorMessage && <Alert severity="error">{viewModel.errorMessage}</Alert>}

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
            select
            name="farmRoleTypeId"
            label="Role"
            fullWidth
            required
            disabled={viewModel.isLoadingRoles}
            value={viewModel.farmRoleTypeId}
            onChange={viewModel.setFarmRoleTypeId}
          >
            {viewModel.roleOptions.map((role) => (
              <MenuItem key={role.id} value={String(role.id)}>
                {role.name}
              </MenuItem>
            ))}
          </FormTextField>

          <FormSubmitButton variant="contained" fullWidth>
            Send Invitation
          </FormSubmitButton>
        </Stack>
      </Form>
    </div>
  );
});
