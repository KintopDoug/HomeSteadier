import { observer } from "mobx-react-lite";
import Typography from "@mui/material/Typography";

export const Garden = observer(() => {
  return (
    <div className="garden-page">
      <Typography variant="h4" component="h1">
        Garden Management
      </Typography>
    </div>
  );
});
