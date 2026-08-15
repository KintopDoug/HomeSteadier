import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { Link } from "@tanstack/react-router";
import AppBar from "@mui/material/AppBar";
import Toolbar from "@mui/material/Toolbar";
import Avatar from "@mui/material/Avatar";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { session } from "../../stores/SessionStore";
import { HeaderViewModel } from "../../viewModels/HeaderViewModel";

export const Header = observer(() => {
  const viewModel = useMemo(() => new HeaderViewModel(), []);

  return (
    <AppBar
      position="static"
      color="default"
      elevation={0}
      sx={{ backgroundColor: "background.paper", borderBottom: 1, borderColor: "divider" }}
    >
      <Toolbar disableGutters sx={{ px: { xs: 2, sm: 3 }, py: 1, gap: 2 }}>
        <Box
          component={Link}
          to="/"
          sx={{ display: "flex", alignItems: "center", textDecoration: "none" }}
        >
          <img
            className="site-header-logo"
            src="/HomeSteadier-icon-with-text.svg"
            alt="HomeSteadier"
          />
        </Box>

        <Box sx={{ flexGrow: 1 }} />

        {session.isInitializing ? null : session.isAuthenticated ? (
          <Stack direction="row" spacing={2} sx={{ alignItems: "center" }}>
            <Button component={Link} to="/home" color="inherit">
              Home
            </Button>

            <Button component={Link} to="/change-password" color="inherit">
              Change Password
            </Button>

            <Stack
              direction="row"
              spacing={1}
              sx={{ alignItems: "center", display: { xs: "none", sm: "flex" } }}
            >
              <Avatar
                sx={{ width: 32, height: 32, bgcolor: "primary.main", fontSize: "0.875rem" }}
              >
                {session.user?.email?.[0]?.toUpperCase()}
              </Avatar>
              <Typography variant="body2" color="text.secondary">
                {session.user?.email}
              </Typography>
            </Stack>

            <Button
              variant="outlined"
              size="small"
              onClick={viewModel.logout}
              disabled={viewModel.isLoggingOut}
            >
              Sign Out
            </Button>
          </Stack>
        ) : null}
      </Toolbar>
    </AppBar>
  );
});
