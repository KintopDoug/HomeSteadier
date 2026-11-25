import { observer } from "mobx-react-lite";
import Alert from "@mui/material/Alert";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { FarmCreateForm } from "../components/Farm/FarmCreateForm";
import { session } from "../stores/SessionStore";

export const EditFarm = observer(() => {
  const farm = session.activeFarm;

  if (farm?.roleName !== "Admin") {
    return (
      <Stack spacing={2} sx={{ maxWidth: 400, mx: "auto" }}>
        <Typography variant="h4" component="h1" align="center">
          Edit Farm
        </Typography>
        <Alert severity="error">Only a farm admin can edit farm details.</Alert>
      </Stack>
    );
  }

  // Keyed by farm id so switching farms rebuilds the form with that farm's values.
  return <FarmCreateForm key={farm.id} farm={farm} />;
});
