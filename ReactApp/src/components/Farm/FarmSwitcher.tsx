import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import ArrowDropDownIcon from "@mui/icons-material/ArrowDropDown";
import Box from "@mui/material/Box";
import Button from "@mui/material/Button";
import Menu from "@mui/material/Menu";
import MenuItem from "@mui/material/MenuItem";
import { session } from "../../stores/SessionStore";
import { FarmSwitcherViewModel } from "../../viewModels/FarmSwitcherViewModel";

/**
 * Header control for switching the active farm. Renders nothing when the user belongs to zero or
 * one farm — there's nothing to switch between (Home handles those cases: farm creation or
 * auto-selecting the single farm).
 */
export const FarmSwitcher = observer(() => {
  const viewModel = useMemo(() => new FarmSwitcherViewModel(), []);

  if (session.farms.length <= 1) {
    return null;
  }

  return (
    <>
      <Button
        onClick={(event) => viewModel.openMenu(event.currentTarget)}
        color="inherit"
        endIcon={<ArrowDropDownIcon />}
        sx={{ minWidth: 0, maxWidth: { xs: 110, sm: "none" }, whiteSpace: "nowrap" }}
      >
        <Box component="span" sx={{ overflow: "hidden", textOverflow: "ellipsis" }}>
          {session.activeFarm?.name ?? "Select Farm"}
        </Box>
      </Button>

      <Menu
        anchorEl={viewModel.menuAnchor}
        open={Boolean(viewModel.menuAnchor)}
        onClose={viewModel.closeMenu}
        anchorOrigin={{ vertical: "bottom", horizontal: "left" }}
        transformOrigin={{ vertical: "top", horizontal: "left" }}
      >
        {session.farms.map((farm) => (
          <MenuItem
            key={farm.id}
            selected={farm.id === session.activeFarm?.id}
            onClick={() => viewModel.selectFarm(farm)}
          >
            {farm.name}
          </MenuItem>
        ))}
      </Menu>
    </>
  );
});
