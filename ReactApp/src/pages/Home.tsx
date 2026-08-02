import { observer } from "mobx-react-lite";
import Typography from "@mui/material/Typography";
import { session } from "../stores/SessionStore";

export const Home = observer(() => {
  return (
    <div className="home-page">
      <Typography variant="h4" component="h1">
        Welcome back
        {session.user?.firstName ? `, ${session.user.firstName}` : ""}!
      </Typography>
    </div>
  );
});
