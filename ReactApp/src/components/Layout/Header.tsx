import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { Link } from "@tanstack/react-router";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { session } from "../../stores/SessionStore";
import { HeaderViewModel } from "../../viewModels/HeaderViewModel";

export const Header = observer(() => {
  const viewModel = useMemo(() => new HeaderViewModel(), []);

  return (
    <header className="site-header">
      <img
        className="site-header-logo"
        src="/HomeSteadier-icon-with-text.svg"
        alt="HomeSteadier"
      />

      <Stack
        direction="row"
        spacing={2}
        sx={{ alignItems: "center" }}
        className="site-header-actions"
      >
        {session.isInitializing ? null : session.isAuthenticated ? (
          <>
            <Link to="/home">Home</Link>
            <Typography variant="body2">
              Signed in as {session.user?.email}
            </Typography>
            <Button
              variant="outlined"
              size="small"
              onClick={viewModel.logout}
              disabled={viewModel.isLoggingOut}
            >
              Sign Out
            </Button>
          </>
        ) : null}
      </Stack>
    </header>
  );
});
