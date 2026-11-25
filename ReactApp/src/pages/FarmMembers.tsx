import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import Alert from "@mui/material/Alert";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import Dialog from "@mui/material/Dialog";
import DialogActions from "@mui/material/DialogActions";
import DialogContent from "@mui/material/DialogContent";
import DialogContentText from "@mui/material/DialogContentText";
import DialogTitle from "@mui/material/DialogTitle";
import MenuItem from "@mui/material/MenuItem";
import Paper from "@mui/material/Paper";
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
          <Alert severity="error">Only a farm admin can manage farm members.</Alert>
        </Stack>
      </div>
    );
  }

  return (
    <div className="farm-members-page">
      <Stack spacing={2} sx={{ maxWidth: 900, mx: "auto" }}>
        <Typography variant="h4" component="h1" align="center">
          {session.activeFarm.name} Members
        </Typography>

        {viewModel.errorMessage && <Alert severity="error">{viewModel.errorMessage}</Alert>}

        {viewModel.isLoading ? (
          <CircularProgress sx={{ alignSelf: "center" }} />
        ) : (
          <TableContainer component={Paper}>
            <Table aria-label="Farm members">
              <TableHead>
                <TableRow>
                  <TableCell>Name</TableCell>
                  <TableCell>Email</TableCell>
                  <TableCell>Role</TableCell>
                  <TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {viewModel.members.map((member) => (
                  <TableRow key={member.userId}>
                    <TableCell>
                      {member.firstName} {member.lastName}
                    </TableCell>
                    <TableCell>{member.email}</TableCell>
                    <TableCell>
                      <Select
                        size="small"
                        value={String(member.farmRoleTypeId)}
                        disabled={viewModel.busyUserId === member.userId}
                        onChange={(e) => viewModel.changeRole(member, e.target.value)}
                        inputProps={{ "aria-label": `Role for ${member.email}` }}
                      >
                        {viewModel.roleOptions.map((role) => (
                          <MenuItem key={role.id} value={String(role.id)}>
                            {role.name}
                          </MenuItem>
                        ))}
                      </Select>
                    </TableCell>
                    <TableCell align="right">
                      <Button
                        color="error"
                        disabled={viewModel.busyUserId === member.userId}
                        onClick={() => viewModel.requestRemoval(member)}
                      >
                        Remove
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        )}
      </Stack>

      <Dialog open={viewModel.memberPendingRemoval !== null} onClose={() => viewModel.requestRemoval(null)}>
        <DialogTitle>Remove member?</DialogTitle>
        <DialogContent>
          <DialogContentText>
            {viewModel.memberPendingRemoval?.email} will lose access to {session.activeFarm.name}.
          </DialogContentText>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => viewModel.requestRemoval(null)}>Cancel</Button>
          <Button color="error" onClick={viewModel.confirmRemoval}>
            Remove
          </Button>
        </DialogActions>
      </Dialog>
    </div>
  );
});
