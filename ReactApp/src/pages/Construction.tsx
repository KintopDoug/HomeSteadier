import { observer } from "mobx-react-lite";
import Typography from "@mui/material/Typography";

export const Construction = observer(() => {
  return (
    <div className="construction-page">
      <Typography variant="h4" component="h1">
        Construction Project Management
      </Typography>
    </div>
  );
});
