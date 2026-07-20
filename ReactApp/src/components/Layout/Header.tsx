import { Show, SignInButton, SignUpButton, UserButton } from "@clerk/react";

export const Header = () => {
  return (
    <header className="site-header">
      <img
        className="site-header-logo"
        src="/HomeSteadier-icon-with-text.svg"
        alt="HomeSteadier"
      />
      <div className="site-header-auth">
        <Show when="signed-out">
          <SignInButton mode="modal" />
          <SignUpButton mode="modal" />
        </Show>
        <Show when="signed-in">
          <UserButton />
        </Show>
      </div>
    </header>
  );
};
