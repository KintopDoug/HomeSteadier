import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import MenuItem from "@mui/material/MenuItem";
import Select from "@mui/material/Select";
import Stack from "@mui/material/Stack";
import Table from "@mui/material/Table";
import TableBody from "@mui/material/TableBody";
import TableCell from "@mui/material/TableCell";
import TableContainer from "@mui/material/TableContainer";
import TableHead from "@mui/material/TableHead";
import TableRow from "@mui/material/TableRow";
import Typography from "@mui/material/Typography";
import { FarmMembersViewModel } from "../viewModels/FarmMembersViewModel";
import { session } from "../stores/SessionStore";

export const FarmMembers = observer(() => {
  const activeFarmId = session.activeFarm?.id;
  // Recreate the view model when the active farm changes so the list and mutations never target different farms.
  const viewModel = useMemo(() => {
    const vm = new FarmMembersViewModel();
    vm.initialize();
    return vm;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeFarmId]);

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
      <Stack spacing={2} sx={{ maxWidth: 800, mx: "auto" }}>
        <Typography variant="h4" component="h1" align="center">
          {session.activeFarm.name} Members
        </Typography>

        {viewModel.errorMessage && <Alert severity="error">{viewModel.errorMessage}</Alert>}

        {viewModel.isLoading ? (
          <CircularProgress sx={{ alignSelf: "center" }} />
        ) : (
          <TableContainer>
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
                {viewModel.members.map((member) => {
                  const isSelf = viewModel.isCurrentUser(member);
                  const isBusy = viewModel.busyUserId === member.userId;
                  return (
                    <TableRow key={member.userId}>
                      <TableCell>
                        {member.firstName} {member.lastName}
                        {isSelf ? " (you)" : ""}
                      </TableCell>
                      <TableCell>{member.email}</TableCell>
                      <TableCell>
                        <Select
                          size="small"
                          value={String(member.farmRoleTypeId)}
                          disabled={isSelf || isBusy}
                          onChange={(event) => viewModel.changeRole(member, event.target.value)}
                          inputProps={{ "aria-label": `Role for ${member.email}` }}
                        >
                          {viewModel.roles.map((role) => (
                            <MenuItem key={role.id} value={String(role.id)}>
                              {role.name}
                            </MenuItem>
                          ))}
                        </Select>
                      </TableCell>
                      <TableCell align="right">
                        <Button
                          color="error"
                          size="small"
                          disabled={isSelf || isBusy}
                          onClick={() => {
                            if (window.confirm(`Remove ${member.email} from ${session.activeFarm?.name}?`)) {
                              viewModel.remove(member);
                            }
                          }}
                        >
                          Remove
                        </Button>
                      </TableCell>
                    </TableRow>
                  );
                })}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Stack>
    </div>
  );
});
