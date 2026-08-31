import { observer } from "mobx-react-lite";
import Box from "@mui/material/Box";
import Typography from "@mui/material/Typography";
import CardActionArea from "@mui/material/CardActionArea";
import YardIcon from "@mui/icons-material/Yard";
import type { FarmResponse } from "../../models/response/FarmResponse";

interface FarmPickerProps {
  farms: FarmResponse[];
  onSelect: (farm: FarmResponse) => void;
}

export const FarmPicker = observer(({ farms, onSelect }: FarmPickerProps) => {
  return (
    <Box className="farm-picker">
      <Typography variant="h4" component="h1" align="center" sx={{ mb: 4 }}>
        Choose a Farm
      </Typography>
      <Box
        sx={{
          display: "flex",
          flexWrap: "wrap",
          justifyContent: "center",
          gap: 3,
        }}
      >
        {farms.map((farm) => (
          <Box
            key={farm.id}
            sx={{
              width: { xs: "100%", sm: 220 },
              height: { xs: 140, sm: 180 },
            }}
          >
            <CardActionArea
              onClick={() => onSelect(farm)}
              sx={{
                height: "100%",
                width: "100%",
                borderRadius: 4,
                bgcolor: "secondary.main",
                color: "secondary.contrastText",
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
                gap: 1.5,
                p: 2,
                textAlign: "center",
              }}
            >
              <YardIcon sx={{ fontSize: 56 }} />
              <Typography variant="h6" component="span">
                {farm.name}
              </Typography>
              {(farm.city || farm.state) && (
                <Typography variant="body2">
                  {[farm.city, farm.state].filter(Boolean).join(", ")}
                </Typography>
              )}
            </CardActionArea>
          </Box>
        ))}
      </Box>
    </Box>
  );
});
