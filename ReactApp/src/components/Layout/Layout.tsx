import type { ReactNode } from "react";
import { Header } from "./Header";

export const Layout = ({ children }: { children: ReactNode }) => {
  return (
    <div className="app-shell">
      <Header />
      {children}
    </div>
  );
};
