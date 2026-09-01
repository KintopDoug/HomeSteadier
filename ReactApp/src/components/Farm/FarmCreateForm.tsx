import { useMemo } from "react";
import { observer } from "mobx-react-lite";
import { z } from "zod";
import Alert from "@mui/material/Alert";
import CircularProgress from "@mui/material/CircularProgress";
import Stack from "@mui/material/Stack";
import Typography from "@mui/material/Typography";
import { Form, FormSubmitButton, FormTextField } from "../Form";
import { CreateFarmViewModel } from "../../viewModels/CreateFarmViewModel";

const createFarmSchema = z.object({
  name: z.string().min(1, "Farm name is required"),
  addressLine: z.string(),
  city: z.string(),
  state: z.string(),
  postalCode: z.string(),
  country: z.string(),
});

export const FarmCreateForm = observer(() => {
  const viewModel = useMemo(() => new CreateFarmViewModel(), []);

  return (
    <div className="farm-create-form">
      <Form schema={createFarmSchema} values={viewModel.values} onSubmit={viewModel.submit}>
        <Typography variant="h4" component="h1" align="center" sx={{ mb: 1 }}>
          Create Your Farm
        </Typography>
        <Typography align="center" sx={{ mb: 3 }}>
          You're not associated with any farms yet. Create one to get started.
        </Typography>

        <Stack spacing={2}>
          {viewModel.errorMessage && <Alert severity="error">{viewModel.errorMessage}</Alert>}

          <FormTextField
            name="name"
            label="Farm Name"
            fullWidth
            required
            value={viewModel.name}
            onChange={viewModel.setName}
          />

          <FormTextField
            name="addressLine"
            label="Address"
            fullWidth
            value={viewModel.addressLine}
            onChange={viewModel.setAddressLine}
          />

          <Stack direction={{ xs: "column", sm: "row" }} spacing={2}>
            <FormTextField
              name="city"
              label="City"
              fullWidth
              value={viewModel.city}
              onChange={viewModel.setCity}
            />
            <FormTextField
              name="state"
              label="State"
              fullWidth
              value={viewModel.state}
              onChange={viewModel.setState}
            />
            <FormTextField
              name="postalCode"
              label="Postal Code"
              fullWidth
              value={viewModel.postalCode}
              onChange={viewModel.setPostalCode}
            />
          </Stack>

          <FormTextField
            name="country"
            label="Country"
            fullWidth
            value={viewModel.country}
            onChange={viewModel.setCountry}
          />

          {viewModel.isGeocoding && (
            <Stack direction="row" spacing={1} sx={{ alignItems: "center" }}>
              <CircularProgress size={16} />
              <Typography variant="body2" color="text.secondary">
                Locating address...
              </Typography>
            </Stack>
          )}

          {!viewModel.isGeocoding && viewModel.geocodeError && (
            <Alert severity="warning">{viewModel.geocodeError}</Alert>
          )}

          {!viewModel.isGeocoding && viewModel.resolvedDisplayName && (
            <Alert severity="success">Located: {viewModel.resolvedDisplayName}</Alert>
          )}

          <FormSubmitButton variant="contained" fullWidth disabled={viewModel.isGeocoding}>
            Create Farm
          </FormSubmitButton>
        </Stack>
      </Form>
    </div>
  );
});
