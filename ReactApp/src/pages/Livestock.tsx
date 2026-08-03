import { observer } from "mobx-react-lite";
import Typography from "@mui/material/Typography";

export const Livestock = observer(() => {
  return (
    <div className="livestock-page">
      <Typography variant="h4" component="h1">
        Livestock Management
      </Typography>
    </div>
  );
});
