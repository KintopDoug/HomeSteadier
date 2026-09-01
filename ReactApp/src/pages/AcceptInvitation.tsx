import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Link, getRouteApi } from "@tanstack/react-router";
import { AcceptInvitationViewModel } from "../viewModels/AcceptInvitationViewModel";

// Via getRouteApi rather than importing Route from ./routes/accept-invitation: the route module
// already imports this page, so that would be a circular import.
const route = getRouteApi("/accept-invitation");

export const AcceptInvitation = observer(() => {
  const { token } = route.useSearch();

  const viewModel = useMemo(() => {
    const vm = new AcceptInvitationViewModel(token);
    vm.initialize();
    return vm;
  }, [token]);

  if (!token) {
    return (
      <div className="accept-invitation-page">
        <Stack spacing={2} sx={{ maxWidth: 320, mx: "auto" }}>
          <Typography variant="h4" component="h1" align="center">
            Invalid Link
          </Typography>
          <Alert severity="error">
            This invitation link is missing its token. It may have been truncated by your email
            client — try copying the whole link.
          </Alert>
        </Stack>
      </div>
    );
  }

  if (viewModel.isLoading) {
    return (
      <Box sx={{ display: "flex", justifyContent: "center", pt: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (viewModel.isAccepted && viewModel.details) {
    return (
      <div className="accept-invitation-page">
        <Stack spacing={2} sx={{ maxWidth: 400, mx: "auto" }}>
          <Typography variant="h4" component="h1" align="center">
            You're In!
          </Typography>
          <Alert severity="success">
            You've been added to {viewModel.details.farmName} as {viewModel.details.roleName}.
          </Alert>
          <Typography align="center">
            <Link to="/login">Sign in to get started</Link>
          </Typography>
        </Stack>
      </div>
    );
  }

  if (viewModel.errorMessage || !viewModel.details) {
    return (
      <div className="accept-invitation-page">
        <Stack spacing={2} sx={{ maxWidth: 400, mx: "auto" }}>
          <Typography variant="h4" component="h1" align="center">
            Invalid Invitation
          </Typography>
          <Alert severity="error">
            {viewModel.errorMessage ?? "This invitation is invalid or has expired."}
          </Alert>
        </Stack>
      </div>
    );
  }

  return (
    <div className="accept-invitation-page">
      <Stack spacing={2} sx={{ maxWidth: 400, mx: "auto" }}>
        <Typography variant="h4" component="h1" align="center">
          Farm Invitation
        </Typography>

        <Typography align="center">
          You've been invited to join <strong>{viewModel.details.farmName}</strong> as{" "}
          <strong>{viewModel.details.roleName}</strong>.
        </Typography>

        <Button
          variant="contained"
          fullWidth
          onClick={viewModel.accept}
          disabled={viewModel.isAccepting}
        >
          Accept Invitation
        </Button>
      </Stack>
    </div>
  );
});
