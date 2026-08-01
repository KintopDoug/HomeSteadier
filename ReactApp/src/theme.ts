import { createTheme } from "@mui/material/styles";
import type { Theme } from "@mui/material/styles";

declare module "@mui/material/styles" {
  interface Palette {
    tertiary: Palette["primary"];
  }
  interface PaletteOptions {
    tertiary?: PaletteOptions["primary"];
  }
}

declare module "@mui/material/Button" {
  interface ButtonPropsColorOverrides {
    tertiary: true;
  }
}

declare module "@mui/material/FormLabel" {
  interface FormLabelPropsColorOverrides {
    black: true;
  }
}

// Colors sourced from the HomeSteadier logo (public/HomeSteadier-icon-with-text.svg):
// rust orange (rooster), sage green (egg + subtitle), gold (sun).
const baseTheme = createTheme({
  palette: {
    primary: {
      main: "#C7742F",
    },
    secondary: {
      main: "#6B7F4B",
    },
  },
});

// `tertiary` isn't a palette key MUI knows about, so light/dark/contrastText
// have to be derived explicitly via augmentColor rather than relying on the
// automatic augmentation primary/secondary get from createTheme.
export const theme = createTheme(baseTheme, {
  palette: {
    tertiary: baseTheme.palette.augmentColor({
      color: { main: "#D9A441" },
      name: "tertiary",
    }),
  },
  components: {
    // InputLabel renders an actual FormLabel internally, so a plain
    // `styleOverrides.root` here would also recolor FormTextField's
    // floating label. Gating on `color="black"` scopes this to FormLabel
    // instances that opt in explicitly (see SignUp.tsx) and leaves
    // FormTextField's label at MUI's default.
    MuiFormLabel: {
      variants: [
        {
          props: { color: "black" },
          style: {
            color: "#000000",
            fontWeight:"bold",
            "&.Mui-focused": {
              color: "#000000",
            },
            "&.Mui-error": {
              color: "#000000",
            },
          },
        },
      ],
    },
    // InputLabel is `styled(FormLabel, ...)`, so it also gets FormLabel's
    // own built-in per-palette-color focus variant (`&.Mui-focused { color:
    // primary.main }`), rendered on the same DOM node as this override.
    // Both land in the stylesheet with equal specificity, so `!important`
    // is needed to reliably win regardless of injection order and keep the
    // label at its normal resting color instead of turning primary/orange.
    MuiInputLabel: {
      styleOverrides: {
        root: ({ theme }: { theme: Theme }) => ({
          "&.Mui-focused:not(.Mui-error)": {
            color: `${theme.palette.text.secondary} !important`,
          },
        }),
      },
    },
    // Same per-color focus variant pattern as above, applied to the
    // outlined text field's border instead of the label.
    MuiOutlinedInput: {
      styleOverrides: {
        root: ({ theme }: { theme: Theme }) => ({
          "&.Mui-focused:not(.Mui-error) .MuiOutlinedInput-notchedOutline": {
            borderColor:
              theme.palette.mode === "light" ? "rgba(0, 0, 0, 0.23)" : "rgba(255, 255, 255, 0.23)",
            borderWidth: 1,
          },
        }),
      },
    },
  },
});
