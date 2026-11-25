import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { Link } from "@tanstack/react-router";
import AppBar from "@mui/material/AppBar";
import Toolbar from "@mui/material/Toolbar";
import Avatar from "@mui/material/Avatar";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import ButtonBase from "@mui/material/ButtonBase";
import Menu from "@mui/material/Menu";
import MenuItem from "@mui/material/MenuItem";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { session } from "../../stores/SessionStore";
import { HeaderViewModel } from "../../viewModels/HeaderViewModel";
import { FarmSwitcher } from "../Farm/FarmSwitcher";

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
          sx={{ display: "flex", alignItems: "center", textDecoration: "none", flexShrink: 0 }}
        >
          <img
            className="site-header-logo"
            src="/HomeSteadier-icon-with-text.svg"
            alt="HomeSteadier"
          />
        </Box>

        <Box sx={{ flexGrow: 1 }} />

        {session.isInitializing ? null : session.isAuthenticated ? (
          <Stack
            direction="row"
            spacing={{ xs: 0.5, sm: 2 }}
            sx={{ alignItems: "center", minWidth: 0 }}
          >
            <Button
              component={Link}
              to="/home"
              color="inherit"
              sx={{ display: { xs: "none", sm: "inline-flex" } }}
            >
              Home
            </Button>

            <FarmSwitcher />

            <ButtonBase
              onClick={(event) => viewModel.openUserMenu(event.currentTarget)}
              aria-label="Open user menu"
              sx={{ borderRadius: 999 }}
            >
              <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
                <Avatar
                  sx={{ width: 32, height: 32, bgcolor: "primary.main", fontSize: "0.875rem" }}
                >
                  {session.user?.email?.[0]?.toUpperCase()}
                </Avatar>
                {/* The avatar is the only entry point to the user menu, so it must stay visible
                    on mobile; only the email text is dropped on narrow screens. */}
                <Typography
                  variant="body2"
                  color="text.secondary"
                  sx={{ display: { xs: "none", sm: "block" } }}
                >
                  {session.user?.email}
                </Typography>
              </Stack>
            </ButtonBase>

            <Menu
              anchorEl={viewModel.userMenuAnchor}
              open={Boolean(viewModel.userMenuAnchor)}
              onClose={viewModel.closeUserMenu}
              anchorOrigin={{ vertical: "bottom", horizontal: "right" }}
              transformOrigin={{ vertical: "top", horizontal: "right" }}
            >
              {/* Home moves into the menu on narrow screens to leave room for the farm name. */}
              <MenuItem
                component={Link}
                to="/home"
                onClick={viewModel.closeUserMenu}
                sx={{ display: { xs: "flex", sm: "none" } }}
              >
                Home
              </MenuItem>

              {session.activeFarm?.roleName === "Admin" && (
                <MenuItem
                  component={Link}
                  to="/invite-member"
                  onClick={viewModel.closeUserMenu}
                >
                  Invite to Farm
                </MenuItem>
              )}

              {session.activeFarm?.roleName === "Admin" && (
                <MenuItem
                  component={Link}
                  to="/edit-farm"
                  onClick={viewModel.closeUserMenu}
                >
                  Edit Farm
                </MenuItem>
              )}

              <MenuItem
                component={Link}
                to="/change-password"
                onClick={viewModel.closeUserMenu}
              >
                Change Password
              </MenuItem>
            </Menu>

            <Button
              variant="outlined"
              size="small"
              onClick={viewModel.logout}
              sx={{ whiteSpace: "nowrap", flexShrink: 0 }}
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
