import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { useNavigate } from "@tanstack/react-router";
import Alert from "@mui/material/Alert";
import Box from "@mui/material/Box";
import CircularProgress from "@mui/material/CircularProgress";
import Typography from "@mui/material/Typography";
import CardActionArea from "@mui/material/CardActionArea";
import type { SvgIconComponent } from "@mui/icons-material";
import YardIcon from "@mui/icons-material/Yard";
import PetsIcon from "@mui/icons-material/Pets";
import ConstructionIcon from "@mui/icons-material/Construction";
import { session } from "../stores/SessionStore";
import { HomeViewModel } from "../viewModels/HomeViewModel";
import { FarmCreateForm } from "../components/Farm/FarmCreateForm";
import { FarmPicker } from "../components/Farm/FarmPicker";

type Section = {
  label: string;
  path: string;
  color: "primary" | "secondary" | "tertiary";
  Icon: SvgIconComponent;
  disabled?: boolean;
};

const sections: Section[] = [
  {
    label: "Garden Assistant",
    path: "/garden",
    color: "secondary",
    Icon: YardIcon,
  },
  {
    label: "Livestock Management",
    path: "/livestock",
    color: "primary",
    Icon: PetsIcon,
    disabled: true,
  },
  {
    label: "Construction Project Management",
    path: "/construction",
    color: "tertiary",
    Icon: ConstructionIcon,
    disabled: true,
  },
];

export const Home = observer(() => {
  const navigate = useNavigate();
  const viewModel = useMemo(() => {
    const vm = new HomeViewModel();
    vm.initialize();
    return vm;
  }, []);

  if (viewModel.isLoading) {
    return (
      <Box className="home-page" sx={{ display: "flex", justifyContent: "center", pt: 8 }}>
        <CircularProgress />
      </Box>
    );
  }

  if (viewModel.errorMessage) {
    return (
      <Box className="home-page" sx={{ display: "flex", justifyContent: "center", pt: 8 }}>
        <Alert severity="error">{viewModel.errorMessage}</Alert>
      </Box>
    );
  }

  if (viewModel.needsFarmCreation) {
    return <FarmCreateForm />;
  }

  if (viewModel.needsFarmSelection) {
    return <FarmPicker farms={viewModel.farms} onSelect={viewModel.selectFarm} />;
  }

  return (
    <Box className="home-page">
      <Typography variant="h4" component="h1" sx={{ mb: 4 }}>
        Welcome back
        {session.user?.firstName ? `, ${session.user.firstName}` : ""}!
        {session.activeFarm?.name ? ` (${session.activeFarm.name})` : ""}
      </Typography>
      <Box
        sx={{
          display: "flex",
          flexWrap: "wrap",
          gap: 3,
        }}
      >
        {sections.map(({ label, path, color, Icon, disabled }) => (
          <Box
            key={path}
            sx={{
              position: "relative",
              width: { xs: "100%", sm: 220 },
              height: { xs: 180, sm: 220 },
            }}
          >
            <CardActionArea
              disabled={disabled}
              onClick={() => navigate({ to: path })}
              sx={{
                height: "100%",
                width: "100%",
                borderRadius: 4,
                bgcolor: `${color}.main`,
                color: `${color}.contrastText`,
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
                gap: 1.5,
                p: 2,
                textAlign: "center",
                "&.Mui-disabled": {
                  bgcolor: `${color}.main`,
                  color: `${color}.contrastText`,
                },
              }}
            >
              <Icon sx={{ fontSize: 72 }} />
              <Typography variant="h6" component="span">
                {label}
              </Typography>
            </CardActionArea>
            {disabled && (
              <Box
                sx={{
                  position: "absolute",
                  inset: 0,
                  borderRadius: 4,
                  bgcolor: "rgba(97, 97, 97, 0.75)",
                  color: "common.white",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  pointerEvents: "none",
                }}
              >
                <Typography variant="h6" component="span">
                  coming soon...
                </Typography>
              </Box>
            )}
          </Box>
        ))}
      </Box>
    </Box>
  );
});
