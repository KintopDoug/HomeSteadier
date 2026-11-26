import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import MenuItem from "@mui/material/MenuItem";
import Select from "@mui/material/Select";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import { FarmMembersViewModel } from "../viewModels/FarmMembersViewModel";
import { session } from "../stores/SessionStore";

export const FarmMembers = observer(() => {
  const viewModel = useMemo(() => {
    const vm = new FarmMembersViewModel();
    vm.initialize();
    return vm;
  }, []);

  if (session.activeFarm?.roleName !== "Admin") {
    return (
      <div className="farm-members-page">
        <Stack spacing={2} sx={{ maxWidth: 400, mx: "auto" }}>
          <Typography variant="h4" component="h1" align="center">
            Farm Members
          </Typography>
          <Alert severity="error">Only a farm admin can manage members.</Alert>
        </Stack>
      </div>
    );
  }

  return (
    <div className="farm-members-page">
      <Stack spacing={2} sx={{ maxWidth: 900, mx: "auto" }}>
        <Typography variant="h4" component="h1" align="center">
          {session.activeFarm.name} members
        </Typography>
        {viewModel.errorMessage && <Alert severity="error">{viewModel.errorMessage}</Alert>}
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Name</TableCell>
              <TableCell>Email</TableCell>
              <TableCell>Role</TableCell>
              <TableCell />
            </TableRow>
          </TableHead>
          <TableBody>
            {viewModel.members.map((member) => (
              <TableRow key={member.userId}>
                <TableCell>{`${member.firstName} ${member.lastName}`}</TableCell>
                <TableCell>{member.email}</TableCell>
                <TableCell>
                  <Select
                    size="small"
                    value={Number(member.farmRoleTypeId)}
                    onChange={(e) => viewModel.changeRole(member, Number(e.target.value))}
                    inputProps={{ "aria-label": `Role for ${member.email}` }}
                  >
                    {viewModel.roleOptions.map((role) => (
                      <MenuItem key={role.id} value={Number(role.id)}>
                        {role.name}
                      </MenuItem>
                    ))}
                  </Select>
                </TableCell>
                <TableCell align="right">
                  <Button color="error" size="small" onClick={() => viewModel.remove(member)}>
                    Remove
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </Stack>
    </div>
  );
});
